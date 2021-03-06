﻿using System;
using NUnit.Framework;
using System.Threading;
using PubnubApi;
using System.Collections.Generic;
using MockServer;

namespace PubNubMessaging.Tests
{
    [TestFixture]
    public class WhenUnsubscribedToAChannelGroup : TestHarness
    {
        private static string channelGroupName = "hello_my_group";
        private static string authKey = "myAuth";
        private static Pubnub pubnub;
        private static Server server;

        [TestFixtureSetUp]
        public static void Init()
        {
            UnitTestLog unitLog = new Tests.UnitTestLog();
            unitLog.LogLevel = MockServer.LoggingMethod.Level.Verbose;
            server = Server.Instance();
            MockServer.LoggingMethod.MockServerLog = unitLog;
            server.Start();

            if (!PubnubCommon.PAMEnabled) { return; }

            bool receivedGrantMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = PubnubCommon.SecretKey,
                AuthKey = authKey,
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);

            pubnub = createPubNubInstance(config);

            string expected = "{\"message\":\"Success\",\"payload\":{\"level\":\"user\",\"subscribe_key\":\"demo-36\",\"ttl\":20,\"channel-group\":\"hello_my_group\",\"auths\":{\"myAuth\":{\"r\":1,\"w\":1,\"m\":1}}},\"service\":\"Access Manager\",\"status\":200}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(string.Format("/v2/auth/grant/sub-key/{0}", PubnubCommon.SubscribeKey))
                    .WithParameter("auth", authKey)
                    .WithParameter("channel-group", channelGroupName)
                    .WithParameter("m", "1")
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("r", "1")
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("ttl", "20")
                    .WithParameter("uuid", config.Uuid)
                    .WithParameter("w", "1")
                    .WithParameter("signature", "mnWJN7WSbajMt_LWpuiXGhcs3NUcVbU3L_MZpb9_blU=")
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent grantManualEvent = new ManualResetEvent(false);
            pubnub.Grant().ChannelGroups(new [] { channelGroupName }).AuthKeys(new [] { authKey }).Read(true).Write(true).Manage(true).TTL(20)
                .Async(new PNAccessManagerGrantResultExt(
                                (r, s) =>
                                {
                                    try
                                    {
                                        Console.WriteLine("PNStatus={0}", pubnub.JsonPluggableLibrary.SerializeToJsonString(s));
                                        if (r != null)
                                        {
                                            Console.WriteLine("PNAccessManagerGrantResult={0}", pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                            if (r.ChannelGroups != null && r.ChannelGroups.Count > 0)
                                            {
                                                foreach (KeyValuePair<string, Dictionary<string, PNAccessManagerKeyData>> channelGroupKP in r.ChannelGroups)
                                                {
                                                    string channelGroup = channelGroupKP.Key;
                                                    var read = r.ChannelGroups[channelGroup][authKey].ReadEnabled;
                                                    var write = r.ChannelGroups[channelGroup][authKey].WriteEnabled;
                                                    if (read && write)
                                                    {
                                                        receivedGrantMessage = true;
                                                    }
                                                    else
                                                    {
                                                        receivedGrantMessage = false;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch { /* ignore */  }
                                    finally
                                    {
                                        grantManualEvent.Set();
                                    }
                                }));
            Thread.Sleep(1000);
            grantManualEvent.WaitOne();

            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;

            Assert.IsTrue(receivedGrantMessage, "WhenUnsubscribedToAChannelGroup Grant access failed.");
        }

        [TestFixtureTearDown]
        public static void Exit()
        {
            server.Stop();
        }

        [Test]
        public static void ThenShouldReturnUnsubscribedMessage()
        {
            server.RunOnHttps(false);

            bool receivedMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                AuthKey = authKey,
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { if (m != null) { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); } },
                (o, p) => { /* Catch the presence events */ },
                (o, s) => {
                    Console.WriteLine("SubscribeCallback: PNStatus: " + s.StatusCode.ToString());
                    if (s.StatusCode != 200 || s.Error)
                    {
                        subscribeManualEvent.Set();
                        Console.ForegroundColor = ConsoleColor.Red;
                        if (s.ErrorData != null)
                        {
                            Console.WriteLine(s.ErrorData.Information);
                        }
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else if (s.StatusCode == 200 && (s.Category == PNStatusCategory.PNConnectedCategory || s.Category == PNStatusCategory.PNDisconnectedCategory))
                    {
                        receivedMessage = true;
                        subscribeManualEvent.Set();
                    }
                });
            pubnub = createPubNubInstance(config);
            pubnub.AddListener(listenerSubCallack);

            channelGroupName = "hello_my_group";
            string channelName = "hello_my_channel";

            int manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"status\": 200, \"message\": \"OK\", \"service\": \"channel-registry\", \"error\": false}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(string.Format("/v1/channel-registration/sub-key/{0}/channel-group/{1}", PubnubCommon.SubscribeKey, channelGroupName))
                    .WithParameter("add", channelName)
                    .WithParameter("auth", config.AuthKey)
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("uuid", config.Uuid)
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent cgManualEvent = new ManualResetEvent(false);
            pubnub.AddChannelsToChannelGroup().Channels(new [] { channelName }).ChannelGroup(channelGroupName)
                .Async(new PNChannelGroupsAddChannelResultExt((r,s)=> {
                    try
                    {
                        Console.WriteLine("PNStatus={0}", pubnub.JsonPluggableLibrary.SerializeToJsonString(s));
                        if (r != null)
                        {
                            Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                            if (s.StatusCode == 200 && s.Error == false && s.AffectedChannelGroups.Contains(channelGroupName))
                            {
                                receivedMessage = true;
                            }
                        }
                    }
                    catch { /* ignore */ }
                    finally { cgManualEvent.Set(); }
                }));
            cgManualEvent.WaitOne(manualResetEventWaitTimeout); 

            if (receivedMessage)
            {
                receivedMessage = false;
                expected = "{\"t\":{\"t\":\"14839022442039237\",\"r\":7},\"m\":[]}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, ","))
                        .WithParameter("auth", authKey)
                        .WithParameter("channel-group", channelGroupName)
                        .WithParameter("heartbeat", "300")
                        .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                        .WithParameter("requestid", "myRequestId")
                        .WithParameter("tt", "0")
                        .WithParameter("uuid", config.Uuid)
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                expected = "{}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, "," ))
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                pubnub.Subscribe<string>().ChannelGroups(new [] { channelGroupName }).Execute();
                subscribeManualEvent.WaitOne(manualResetEventWaitTimeout); //Wait for Connect Status

                if (receivedMessage)
                {
                    receivedMessage = false;
                    subscribeManualEvent = new ManualResetEvent(false);

                    expected = "{\"status\": 200, \"action\": \"leave\", \"message\": \"OK\", \"service\": \"Presence\"}";

                    server.AddRequest(new Request()
                            .WithMethod("GET")
                            .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channelGroupName))
                            .WithResponse(expected)
                            .WithStatusCode(System.Net.HttpStatusCode.OK));

                    pubnub.Unsubscribe<string>().ChannelGroups(new [] { channelGroupName }).Execute();
                    subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);
                }

                pubnub.RemoveListener(listenerSubCallack);
                pubnub.Destroy();
                pubnub.PubnubUnitTest = null;
                pubnub = null;

                Assert.IsTrue(receivedMessage, "WhenUnsubscribedToAChannelGroup --> ThenShouldReturnUnsubscribedMessage Failed");
            }
            else
            {
                Assert.IsTrue(receivedMessage, "WhenUnsubscribedToAChannelGroup --> ThenShouldReturnUnsubscribedMessage Failed");
            }
        }

    }
}
