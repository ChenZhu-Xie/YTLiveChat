function Wait-BackendReady {
    param(
        [Parameter(Mandatory)]
        [string]$HealthUri,

        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,

        [int]$TimeoutSeconds = 30
    )

    $healthUriObject = [uri]$HealthUri
    $getTcpConnection = Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue
    $foreignListenerProcessIds = @()
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if ($Process.HasExited) {
            if ($foreignListenerProcessIds.Count -gt 0) {
                $owners = $foreignListenerProcessIds | ForEach-Object {
                    $ownerProcess = Get-Process -Id $_ -ErrorAction SilentlyContinue
                    if ($null -ne $ownerProcess) {
                        "$($ownerProcess.ProcessName) (PID $_)"
                    } else {
                        "PID $_"
                    }
                }
                throw "Backend exited because $HealthUri is already served by $($owners -join ', ')."
            }
            throw "Backend exited before becoming ready (exit code $($Process.ExitCode))."
        }

        try {
            $response = Invoke-WebRequest -Uri $HealthUri -Method Get -TimeoutSec 1 -UseBasicParsing
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                if ($null -eq $getTcpConnection) {
                    return
                }

                $listeners = @(Get-NetTCPConnection `
                    -LocalPort $healthUriObject.Port `
                    -State Listen `
                    -ErrorAction SilentlyContinue)
                if ($listeners.OwningProcess -contains $Process.Id) {
                    return
                }

                $foreignListenerProcessIds = @($listeners.OwningProcess | Where-Object { $_ -ne $Process.Id } | Select-Object -Unique)
            }
        }
        catch {
            # The listener may not be ready yet.
        }

        Start-Sleep -Milliseconds 250
    }

    throw "Backend did not become ready at $HealthUri within $TimeoutSeconds seconds."
}

function Send-ObsWebSocketMessage {
    param(
        [Parameter(Mandatory)]
        [System.Net.WebSockets.ClientWebSocket]$Socket,

        [Parameter(Mandatory)]
        [object]$Message
    )

    $json = $Message | ConvertTo-Json -Compress -Depth 10
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $segment = [System.ArraySegment[byte]]::new($bytes)
    $cancellation = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(5))
    try {
        $null = $Socket.SendAsync(
            $segment,
            [System.Net.WebSockets.WebSocketMessageType]::Text,
            $true,
            $cancellation.Token
        ).GetAwaiter().GetResult()
    }
    finally {
        $cancellation.Dispose()
    }
}

function Receive-ObsWebSocketMessage {
    param(
        [Parameter(Mandatory)]
        [System.Net.WebSockets.ClientWebSocket]$Socket
    )

    $buffer = [byte[]]::new(8192)
    $stream = [System.IO.MemoryStream]::new()
    $cancellation = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(5))
    try {
        do {
            $segment = [System.ArraySegment[byte]]::new($buffer)
            $result = $Socket.ReceiveAsync($segment, $cancellation.Token).GetAwaiter().GetResult()
            if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
                throw "OBS closed the WebSocket connection."
            }

            $stream.Write($buffer, 0, $result.Count)
        } while (-not $result.EndOfMessage)

        $json = [System.Text.Encoding]::UTF8.GetString($stream.ToArray())
        return $json | ConvertFrom-Json
    }
    finally {
        $cancellation.Dispose()
        $stream.Dispose()
    }
}

function Invoke-ObsWebSocketRequest {
    param(
        [Parameter(Mandatory)]
        [System.Net.WebSockets.ClientWebSocket]$Socket,

        [Parameter(Mandatory)]
        [string]$RequestType,

        [hashtable]$RequestData = @{}
    )

    $requestId = [guid]::NewGuid().ToString("N")
    Send-ObsWebSocketMessage -Socket $Socket -Message @{
        op = 6
        d = @{
            requestType = $RequestType
            requestId = $requestId
            requestData = $RequestData
        }
    }

    do {
        $response = Receive-ObsWebSocketMessage -Socket $Socket
    } while ($response.op -ne 7 -or $response.d.requestId -ne $requestId)

    if (-not $response.d.requestStatus.result) {
        $comment = $response.d.requestStatus.comment
        throw "OBS request '$RequestType' failed ($($response.d.requestStatus.code)): $comment"
    }

    $responseDataProperty = $response.d.PSObject.Properties["responseData"]
    if ($null -eq $responseDataProperty) {
        return $null
    }
    return $responseDataProperty.Value
}

function Get-ObsWebSocketSettings {
    param(
        [string]$WebSocketUrl,
        [string]$Password
    )

    $configPath = Join-Path $env:APPDATA "obs-studio\plugin_config\obs-websocket\config.json"
    $config = $null
    if (Test-Path -LiteralPath $configPath) {
        try {
            $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
        }
        catch {
            Write-Verbose "Could not read OBS WebSocket configuration: $($_.Exception.Message)"
        }
    }

    if ([string]::IsNullOrWhiteSpace($WebSocketUrl)) {
        $port = if ($null -ne $config -and $config.server_port) {
            [int]$config.server_port
        } else {
            4455
        }
        $WebSocketUrl = "ws://127.0.0.1:$port"
    }

    if ([string]::IsNullOrEmpty($Password) -and $null -ne $config) {
        $Password = [string]$config.server_password
    }

    return @{
        WebSocketUrl = $WebSocketUrl
        Password = $Password
    }
}

function Get-ObsAuthenticationResponse {
    param(
        [Parameter(Mandatory)]
        [string]$Password,

        [Parameter(Mandatory)]
        [string]$Salt,

        [Parameter(Mandatory)]
        [string]$Challenge
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $secretBytes = [System.Text.Encoding]::UTF8.GetBytes($Password + $Salt)
        $secret = [Convert]::ToBase64String($sha256.ComputeHash($secretBytes))
        $authBytes = [System.Text.Encoding]::UTF8.GetBytes($secret + $Challenge)
        return [Convert]::ToBase64String($sha256.ComputeHash($authBytes))
    }
    finally {
        $sha256.Dispose()
    }
}

function Test-ObsBrowserSourceUrl {
    param(
        [string]$CandidateUrl,
        [Parameter(Mandatory)]
        [uri]$ExpectedUri
    )

    if ([string]::IsNullOrWhiteSpace($CandidateUrl)) {
        return $false
    }

    try {
        $candidateUri = [uri]$CandidateUrl
    }
    catch {
        return $false
    }

    $localHosts = @("localhost", "127.0.0.1", "::1")
    $hostsMatch = $candidateUri.Host -eq $ExpectedUri.Host -or
        ($candidateUri.Host -in $localHosts -and $ExpectedUri.Host -in $localHosts)

    return $hostsMatch -and $candidateUri.Port -eq $ExpectedUri.Port
}

function Invoke-ObsBrowserSourceRefresh {
    param(
        [Parameter(Mandatory)]
        [uri]$BrowserSourceUri,

        [string]$PreferredSourceName,
        [string]$WebSocketUrl,
        [string]$Password
    )

    $settings = Get-ObsWebSocketSettings -WebSocketUrl $WebSocketUrl -Password $Password
    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $cancellation = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(3))
    try {
        $null = $socket.ConnectAsync([uri]$settings.WebSocketUrl, $cancellation.Token).GetAwaiter().GetResult()
        $hello = Receive-ObsWebSocketMessage -Socket $socket
        if ($hello.op -ne 0) {
            throw "OBS WebSocket returned an unexpected hello message."
        }

        $identifyData = @{
            rpcVersion = 1
            eventSubscriptions = 0
        }
        $authenticationProperty = $hello.d.PSObject.Properties["authentication"]
        if ($null -ne $authenticationProperty) {
            if ([string]::IsNullOrEmpty($settings.Password)) {
                throw "OBS WebSocket requires a password. Set OBS_WEBSOCKET_PASSWORD or pass -ObsWebSocketPassword."
            }

            $identifyData.authentication = Get-ObsAuthenticationResponse `
                -Password $settings.Password `
                -Salt $authenticationProperty.Value.salt `
                -Challenge $authenticationProperty.Value.challenge
        }

        Send-ObsWebSocketMessage -Socket $socket -Message @{
            op = 1
            d = $identifyData
        }
        $identified = Receive-ObsWebSocketMessage -Socket $socket
        if ($identified.op -ne 2) {
            throw "OBS WebSocket authentication failed."
        }

        $inputList = Invoke-ObsWebSocketRequest -Socket $socket -RequestType "GetInputList" -RequestData @{
            inputKind = "browser_source"
        }
        $matchingNames = [System.Collections.Generic.List[string]]::new()
        foreach ($input in @($inputList.inputs)) {
            $inputSettings = Invoke-ObsWebSocketRequest -Socket $socket -RequestType "GetInputSettings" -RequestData @{
                inputName = $input.inputName
            }
            $candidateUrl = $null
            $settingsProperty = $inputSettings.PSObject.Properties["inputSettings"]
            if ($null -ne $settingsProperty -and $null -ne $settingsProperty.Value) {
                $urlProperty = $settingsProperty.Value.PSObject.Properties["url"]
                if ($null -ne $urlProperty) {
                    $candidateUrl = [string]$urlProperty.Value
                }
            }
            if (-not (Test-ObsBrowserSourceUrl -CandidateUrl $candidateUrl -ExpectedUri $BrowserSourceUri)) {
                continue
            }

            $matchingNames.Add([string]$input.inputName)
        }

        $namesToRefresh = if ([string]::IsNullOrWhiteSpace($PreferredSourceName)) {
            @($matchingNames)
        } else {
            @($matchingNames | Where-Object { $_ -eq $PreferredSourceName })
        }

        $refreshedNames = [System.Collections.Generic.List[string]]::new()
        foreach ($inputName in $namesToRefresh) {
            $null = Invoke-ObsWebSocketRequest -Socket $socket -RequestType "PressInputPropertiesButton" -RequestData @{
                inputName = $inputName
                propertyName = "refreshnocache"
            }
            $refreshedNames.Add($inputName)
        }

        return $refreshedNames.ToArray()
    }
    finally {
        $cancellation.Dispose()
        if ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
            try {
                $null = $socket.CloseAsync(
                    [System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,
                    "Done",
                    [System.Threading.CancellationToken]::None
                ).GetAwaiter().GetResult()
            }
            catch {
                # The refresh has already completed; a close failure is harmless.
            }
        }
        $socket.Dispose()
    }
}

function Complete-BackendLaunch {
    param(
        [Parameter(Mandatory)]
        [string]$ApplicationName,

        [Parameter(Mandatory)]
        [uri]$HealthUri,

        [Parameter(Mandatory)]
        [uri]$BrowserSourceUri,

        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,

        [switch]$SkipObsRefresh,
        [string]$ObsBrowserSourceName,
        [string]$ObsWebSocketUrl,
        [string]$ObsWebSocketPassword
    )

    Wait-BackendReady -HealthUri $HealthUri.AbsoluteUri -Process $Process
    Write-Host "[$ApplicationName] Backend ready at $($BrowserSourceUri.AbsoluteUri)"

    if ($SkipObsRefresh) {
        return
    }

    try {
        $sourceName = if ([string]::IsNullOrWhiteSpace($ObsBrowserSourceName)) {
            $ApplicationName
        } else {
            $ObsBrowserSourceName
        }
        $refreshedNames = @(Invoke-ObsBrowserSourceRefresh `
            -BrowserSourceUri $BrowserSourceUri `
            -PreferredSourceName $sourceName `
            -WebSocketUrl $ObsWebSocketUrl `
            -Password $ObsWebSocketPassword)
        if ($refreshedNames.Count -eq 0) {
            Write-Warning "[$ApplicationName] No OBS browser source named '$sourceName' matched $($BrowserSourceUri.Host):$($BrowserSourceUri.Port)."
        } else {
            Write-Host "[$ApplicationName] Refreshed OBS browser source(s): $($refreshedNames -join ', ')"
        }
    }
    catch {
        Write-Warning "[$ApplicationName] Backend is ready, but the OBS browser source could not be refreshed: $($_.Exception.Message)"
    }
}
