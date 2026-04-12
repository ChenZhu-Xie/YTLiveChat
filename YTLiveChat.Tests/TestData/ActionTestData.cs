namespace YTLiveChat.Tests.TestData;

internal static class ActionTestData
{
    public static string ViewerEngagementSubscribersOnly()
    {
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;
        return $$"""
            {
              "addChatItemAction": {
                "item": {
                  "liveChatViewerEngagementMessageRenderer": {
                    "id": "VE_MSG_TEST_01",
                    "timestampUsec": "{{ts}}",
                    "icon": { "iconType": "YOUTUBE_ROUND" },
                    "message": {
                      "runs": [
                        { "text": "Subscribers-only mode. Messages that appear are from people who've subscribed to this channel for " },
                        { "text": "10 minutes" },
                        { "text": " or longer." }
                      ]
                    }
                  }
                },
                "clientId": "TEST_CLIENT_ID_VE_01"
              }
            }
            """;
    }

    public static string AddBannerPinnedMessage() => """
            {
              "addBannerToLiveChatCommand": {
                "bannerRenderer": {
                  "liveChatBannerRenderer": {
                    "header": {
                      "liveChatBannerHeaderRenderer": {
                        "icon": { "iconType": "KEEP" },
                        "text": {
                          "runs": [
                            { "text": "Pinned by " },
                            { "text": "@Host" }
                          ]
                        }
                      }
                    },
                    "contents": {
                      "liveChatTextMessageRenderer": {
                        "message": { "runs": [{ "text": "Pinned message body" }] },
                        "id": "PINNED_TEXT_ID_01",
                        "authorName": { "simpleText": "@Host" },
                        "authorExternalChannelId": "UC_HOST_01"
                      }
                    },
                    "actionId": "PINNED_ACTION_ID_01",
                    "targetId": "live-chat-banner"
                  }
                }
              }
            }
            """;

    public static string RemoveChatItem() => """
            {
              "removeChatItemAction": {
                "targetItemId": "REMOVED_MSG_ID_01"
              }
            }
            """;

    public static string ReportModerationStateEmpty() => """
            {
              "liveChatReportModerationStateCommand": {}
            }
            """;

    public static string UpdatePollActionWithVotes() => """
            {
              "updateLiveChatPollAction": {
                "pollToUpdate": {
                  "pollRenderer": {
                    "choices": [
                      {
                        "text": { "runs": [{ "text": "Option A" }] },
                        "selected": false,
                        "voteRatio": 0.28,
                        "votePercentage": { "simpleText": "28%" }
                      },
                      {
                        "text": { "runs": [{ "text": "Option B" }] },
                        "selected": false,
                        "voteRatio": 0.72,
                        "votePercentage": { "simpleText": "72%" }
                      }
                    ],
                    "liveChatPollId": "POLL_ID_UPDATE_01",
                    "header": {
                      "pollHeaderRenderer": {
                        "pollQuestion": {},
                        "metadataText": {
                          "runs": [
                            { "text": "@StreamerHandle" },
                            { "text": " \u2022 " },
                            { "text": "2 minutes ago" },
                            { "text": " \u2022 " },
                            { "text": "1,234 votes" }
                          ]
                        },
                        "liveChatPollType": "LIVE_CHAT_POLL_TYPE_CREATOR"
                      }
                    }
                  }
                }
              }
            }
            """;

    public static string ShowPanelActionNewPoll() => """
            {
              "showLiveChatActionPanelAction": {
                "panelToShow": {
                  "liveChatActionPanelRenderer": {
                    "contents": {
                      "pollRenderer": {
                        "choices": [
                          {
                            "text": { "runs": [{ "text": "Yes" }] },
                            "selected": false,
                            "voteRatio": 0,
                            "votePercentage": { "simpleText": "0%" }
                          },
                          {
                            "text": { "runs": [{ "text": "No" }] },
                            "selected": false,
                            "voteRatio": 0,
                            "votePercentage": { "simpleText": "0%" }
                          }
                        ],
                        "liveChatPollId": "POLL_ID_SHOW_01",
                        "header": {
                          "pollHeaderRenderer": {
                            "pollQuestion": {},
                            "metadataText": {
                              "runs": [
                                { "text": "@Creator" },
                                { "text": " \u2022 " },
                                { "text": "just now" },
                                { "text": " \u2022 " },
                                { "text": "0 votes" }
                              ]
                            },
                            "liveChatPollType": "LIVE_CHAT_POLL_TYPE_CREATOR"
                          }
                        }
                      }
                    },
                    "id": "POLL_ID_SHOW_01"
                  }
                }
              }
            }
            """;

    public static string RemoveChatItemByAuthor() => """
            {
              "removeChatItemByAuthorAction": {
                "externalChannelId": "UC_BANNED_CHANNEL_01"
              }
            }
            """;

    public static string RemoveBanner() => """
            {
              "removeBannerForLiveChatCommand": {
                "targetActionId": "PINNED_ACTION_ID_01"
              }
            }
            """;

    public static string CloseLiveChatActionPanel() => """
            {
              "closeLiveChatActionPanelAction": {
                "targetPanelId": "POLL_ID_SHOW_01"
              }
            }
            """;

    public static string ModeChangeMessageRenderer()
    {
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;
        return $$"""
            {
              "addChatItemAction": {
                "item": {
                  "liveChatModeChangeMessageRenderer": {
                    "id": "MODE_CHANGE_TEST_01",
                    "timestampUsec": "{{ts}}",
                    "icon": { "iconType": "QUESTION_ANSWER" },
                    "text": {
                      "runs": [
                        { "text": "@Host", "bold": true },
                        { "text": " turned off subscribers-only mode", "bold": true }
                      ]
                    },
                    "subtext": {
                      "runs": [
                        { "text": "Anyone can send a message", "italics": true }
                      ]
                    }
                  }
                },
                "clientId": "TEST_CLIENT_ID_MODE_01"
              }
            }
            """;
    }

    public static string PlaceholderItemRenderer()
    {
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;
        return $$"""
            {
              "addChatItemAction": {
                "item": {
                  "liveChatPlaceholderItemRenderer": {
                    "id": "PLACEHOLDER_TEST_01",
                    "timestampUsec": "{{ts}}"
                  }
                },
                "clientId": "TEST_CLIENT_ID_PLACEHOLDER_01"
              }
            }
            """;
    }
}
