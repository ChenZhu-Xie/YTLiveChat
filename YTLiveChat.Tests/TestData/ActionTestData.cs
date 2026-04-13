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
                        "timestampUsec": "1776004576422639",
                        "authorName": { "simpleText": "@Host" },
                        "authorExternalChannelId": "UC_HOST_01",
                        "authorPhoto": {
                          "thumbnails": [
                            { "url": "https://yt4.ggpht.com/test_avatar=s32", "width": 32, "height": 32 },
                            { "url": "https://yt4.ggpht.com/test_avatar=s64", "width": 64, "height": 64 }
                          ]
                        },
                        "authorBadges": [
                          {
                            "liveChatAuthorBadgeRenderer": {
                              "icon": { "iconType": "VERIFIED" },
                              "tooltip": "Verified",
                              "accessibility": { "accessibilityData": { "label": "Verified" } }
                            }
                          }
                        ]
                      }
                    },
                    "actionId": "PINNED_ACTION_ID_01",
                    "bannerType": "LIVE_CHAT_BANNER_TYPE_PINNED_MESSAGE",
                    "targetId": "live-chat-banner"
                  }
                }
              }
            }
            """;

    /// <summary>
    /// Cross-channel redirect banner from real live capture (watch_20260412_152414.jsonl).
    /// Banner type: LIVE_CHAT_BANNER_TYPE_CROSS_CHANNEL_REDIRECT
    /// </summary>
    public static string AddBannerRedirectCommand() => """
            {
              "addBannerToLiveChatCommand": {
                "bannerRenderer": {
                  "liveChatBannerRenderer": {
                    "contents": {
                      "liveChatBannerRedirectRenderer": {
                        "bannerMessage": {
                          "runs": [
                            { "text": "Don't miss out! People are going to watch something from ", "fontFace": "FONT_FACE_ROBOTO_REGULAR" },
                            { "text": "@TakanashiKiara", "bold": true, "fontFace": "FONT_FACE_ROBOTO_REGULAR" }
                          ]
                        },
                        "authorPhoto": {
                          "thumbnails": [
                            { "url": "https://yt4.ggpht.com/vnzn_RiKneABPPnp1-0SO4IAZQRXqVsL5RNDQYGR9GhT-Flm47vM4UJeyGfn4U_gteKqJMBwNA=s32-c-k-c0x00ffffff-no-rj", "width": 32, "height": 32 },
                            { "url": "https://yt4.ggpht.com/vnzn_RiKneABPPnp1-0SO4IAZQRXqVsL5RNDQYGR9GhT-Flm47vM4UJeyGfn4U_gteKqJMBwNA=s64-c-k-c0x00ffffff-no-rj", "width": 64, "height": 64 }
                          ]
                        },
                        "inlineActionButton": {
                          "buttonRenderer": {
                            "style": "STYLE_DEFAULT",
                            "size": "SIZE_DEFAULT",
                            "isDisabled": false,
                            "text": { "runs": [{ "text": "Go now" }] },
                            "command": {
                              "commandMetadata": { "webCommandMetadata": { "url": "/watch?v=OcULALBAXRA", "webPageType": "WEB_PAGE_TYPE_WATCH", "rootVe": 3832 } },
                              "watchEndpoint": { "videoId": "OcULALBAXRA" }
                            }
                          }
                        }
                      }
                    },
                    "actionId": "ChwKGkNKLW1yNjd4NkpNREZhRE5GZ2tkVUFNWUNn",
                    "bannerType": "LIVE_CHAT_BANNER_TYPE_CROSS_CHANNEL_REDIRECT",
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

    /// <summary>
    /// Fresh poll from <c>showLiveChatActionPanelAction</c>.
    /// Real polls have NO <c>voteRatio</c>/<c>votePercentage</c> on choices when first opened.
    /// Data matches the shape observed in live captures (dump_showpanel_new.json).
    /// </summary>
    public static string ShowPanelActionNewPoll() => """
            {
              "clickTrackingParams": "CAEQl98BIhMIkq6pmejokwMVHwE6Ah3w5hZNygEEpuqq1w==",
              "showLiveChatActionPanelAction": {
                "panelToShow": {
                  "liveChatActionPanelRenderer": {
                    "contents": {
                      "pollRenderer": {
                        "choices": [
                          {
                            "text": { "runs": [{ "text": "LET IN" }] },
                            "selected": false,
                            "signinEndpoint": {
                              "clickTrackingParams": "CAIQiK0HIhMIkq6pmejokwMVHwE6Ah3w5hZNygEEpuqq1w==",
                              "commandMetadata": {
                                "webCommandMetadata": { "webPageType": "WEB_PAGE_TYPE_UNKNOWN", "rootVe": 83769 }
                              },
                              "signInEndpoint": { "nextEndpoint": { "clickTrackingParams": "CAIQiK0HIhMIkq6pmejokwMVHwE6Ah3w5hZNygEEpuqq1w==" } }
                            }
                          },
                          {
                            "text": { "runs": [{ "text": "OUT" }] },
                            "selected": false,
                            "signinEndpoint": {
                              "clickTrackingParams": "CAIQiK0HIhMIkq6pmejokwMVHwE6Ah3w5hZNygEEpuqq1w==",
                              "commandMetadata": {
                                "webCommandMetadata": { "webPageType": "WEB_PAGE_TYPE_UNKNOWN", "rootVe": 83769 }
                              },
                              "signInEndpoint": { "nextEndpoint": { "clickTrackingParams": "CAIQiK0HIhMIkq6pmejokwMVHwE6Ah3w5hZNygEEpuqq1w==" } }
                            }
                          }
                        ],
                        "trackingParams": "CAIQiK0HIhMIkq6pmejokwMVHwE6Ah3w5hZN",
                        "liveChatPollId": "ChwKGkNNdVFxNWpvNkpNREZaekh3Z1FkelJZTExR",
                        "header": {
                          "pollHeaderRenderer": {
                            "pollQuestion": {},
                            "thumbnail": {
                              "thumbnails": [
                                { "url": "https://yt4.ggpht.com/HKYI1ENbRIVyDgLVtpxOKyLAOEdOHWH__-JQu6Kj2dq0S9U-wTccKoZT0-4DBd21O0Cpo6NnlA=s32-c-k-c0x00ffffff-no-rj", "width": 32, "height": 32 },
                                { "url": "https://yt4.ggpht.com/HKYI1ENbRIVyDgLVtpxOKyLAOEdOHWH__-JQu6Kj2dq0S9U-wTccKoZT0-4DBd21O0Cpo6NnlA=s64-c-k-c0x00ffffff-no-rj", "width": 64, "height": 64 }
                              ]
                            },
                            "metadataText": {
                              "runs": [
                                { "text": "@holoen_raorapanthera" },
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
                    "id": "ChwKGkNNdVFxNWpvNkpNREZaekh3Z1FkelJZTExR",
                    "targetId": "live-chat-action-panel"
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

    /// <summary>
    /// replaceChatItemAction with a full liveChatTextMessageRenderer replacement.
    /// Data from real live capture (dump_replace_membership.json, first entry).
    /// </summary>
    public static string ReplaceChatItemWithText() => """
            {
              "replaceChatItemAction": {
                "targetItemId": "ChwKGkNQcTJ5X0t3NlpNREZlZkJ3Z1FkUTI4RmJR",
                "replacementItem": {
                  "liveChatTextMessageRenderer": {
                    "message": {
                      "runs": [
                        { "text": "pagi bokobo" }
                      ]
                    },
                    "authorName": { "simpleText": "@asepjulian896" },
                    "authorPhoto": {
                      "thumbnails": [
                        { "url": "https://yt4.ggpht.com/ytc/AIdro_nVlngOB3p8jHEgg5A4A1VRs3m2pHGn2mrA5O1J3ck=s32-c-k-c0x00ffffff-no-rj", "width": 32, "height": 32 },
                        { "url": "https://yt4.ggpht.com/ytc/AIdro_nVlngOB3p8jHEgg5A4A1VRs3m2pHGn2mrA5O1J3ck=s64-c-k-c0x00ffffff-no-rj", "width": 64, "height": 64 }
                      ]
                    },
                    "id": "ChwKGkNQcTJ5X0t3NlpNREZlZkJ3Z1FkUTI4RmJR",
                    "timestampUsec": "1776033641718643",
                    "authorExternalChannelId": "UCFIehAvmitLzMf3KDWlW-sA"
                  }
                }
              }
            }
            """;

    /// <summary>
    /// replaceChatItemAction where the replacement is a liveChatPlaceholderItemRenderer.
    /// Replacement should produce a null ChatItem (placeholder maps to no output).
    /// </summary>
    public static string ReplaceChatItemWithPlaceholder()
    {
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;
        return $$"""
            {
              "replaceChatItemAction": {
                "targetItemId": "REPLACE_TARGET_PLACEHOLDER_01",
                "replacementItem": {
                  "liveChatPlaceholderItemRenderer": {
                    "id": "REPLACE_PLACEHOLDER_01",
                    "timestampUsec": "{{ts}}"
                  }
                }
              }
            }
            """;
    }

    /// <summary>
    /// updateLiveChatPollAction with 0% votes (poll just opened, first update).
    /// Data from real live capture (dump_updatepoll_new.json, first entry).
    /// </summary>
    public static string UpdatePollActionZeroVotes() => """
            {
              "updateLiveChatPollAction": {
                "pollToUpdate": {
                  "pollRenderer": {
                    "choices": [
                      {
                        "text": { "runs": [{ "text": "LET IN" }] },
                        "selected": false,
                        "voteRatio": 0,
                        "votePercentage": { "simpleText": "0%" },
                        "signinEndpoint": { "commandMetadata": { "webCommandMetadata": { "webPageType": "WEB_PAGE_TYPE_UNKNOWN", "rootVe": 83769 } }, "signInEndpoint": { "nextEndpoint": {} } }
                      },
                      {
                        "text": { "runs": [{ "text": "OUT" }] },
                        "selected": false,
                        "voteRatio": 0,
                        "votePercentage": { "simpleText": "0%" },
                        "signinEndpoint": { "commandMetadata": { "webCommandMetadata": { "webPageType": "WEB_PAGE_TYPE_UNKNOWN", "rootVe": 83769 } }, "signInEndpoint": { "nextEndpoint": {} } }
                      }
                    ],
                    "liveChatPollId": "ChwKGkNNdVFxNWpvNkpNREZaekh3Z1FkelJZTExR",
                    "header": {
                      "pollHeaderRenderer": {
                        "pollQuestion": {},
                        "thumbnail": {
                          "thumbnails": [
                            { "url": "https://yt4.ggpht.com/HKYI1ENbRIVyDgLVtpxOKyLAOEdOHWH__-JQu6Kj2dq0S9U-wTccKoZT0-4DBd21O0Cpo6NnlA=s32-c-k-c0x00ffffff-no-rj", "width": 32, "height": 32 },
                            { "url": "https://yt4.ggpht.com/HKYI1ENbRIVyDgLVtpxOKyLAOEdOHWH__-JQu6Kj2dq0S9U-wTccKoZT0-4DBd21O0Cpo6NnlA=s64-c-k-c0x00ffffff-no-rj", "width": 64, "height": 64 }
                          ]
                        },
                        "metadataText": {
                          "runs": [
                            { "text": "@holoen_raorapanthera" },
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
                }
              }
            }
            """;
}
