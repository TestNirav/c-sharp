﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using System.ComponentModel;
using System.Threading;
using System.Collections;
using PubnubApi;
using MockServer;

namespace PubNubMessaging.Tests
{
    [TestFixture]
    public class WhenAClientIsPresented : TestHarness
    {
        private static int manualResetEventWaitTimeout = 310 * 1000;
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
            string channel = "hello_my_channel";
            string authKey = "myAuth";

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

            string expected = "{\"message\":\"Success\",\"payload\":{\"level\":\"user\",\"subscribe_key\":\"demo-36\",\"ttl\":20,\"channel\":\"hello_my_channel\",\"auths\":{\"myAuth\":{\"r\":1,\"w\":1,\"m\":1}}},\"service\":\"Access Manager\",\"status\":200}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(string.Format("/v2/auth/grant/sub-key/{0}", PubnubCommon.SubscribeKey))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent grantManualEvent = new ManualResetEvent(false);
            pubnub.Grant().Channels(new [] { channel }).AuthKeys(new [] { authKey }).Read(true).Write(true).Manage(true).TTL(20)
                .Async(new PNAccessManagerGrantResultExt(
                                (r, s) =>
                                {
                                    try
                                    {
                                        Console.WriteLine("PNStatus={0}", pubnub.JsonPluggableLibrary.SerializeToJsonString(s));
                                        if (r != null)
                                        {
                                            Console.WriteLine("PNAccessManagerGrantResult={0}", pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                            if (r.Channels != null && r.Channels.Count > 0)
                                            {
                                                var read = r.Channels[channel][authKey].ReadEnabled;
                                                var write = r.Channels[channel][authKey].WriteEnabled;
                                                if (read && write) { receivedGrantMessage = true; }
                                            }
                                        }
                                    }
                                    catch { /* ignore */ }
                                    finally
                                    {
                                        grantManualEvent.Set();
                                    }
                                }));

            Thread.Sleep(100);

            grantManualEvent.WaitOne();

            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedGrantMessage, "WhenAClientIsPresent Grant access failed.");
        }

        [TestFixtureTearDown]
        public static void Exit()
        {
            server.Stop();
        }

#if (USE_JSONFX)
        [Test]
#else
        [Ignore]
#endif
        public void UsingJsonFx()
        {
            Console.Write("UsingJsonFx");
            Assert.True(true, "UsingJsonFx");
        }

#if (USE_JSONFX)
        [Ignore]
#else
        [Test]
#endif
        public void UsingNewtonSoft()
        {
            Console.Write("UsingNewtonSoft");
            Assert.True(true, "UsingNewtonSoft");
        }

        [Test]
        public static void ThenPresenceShouldReturnReceivedMessage()
        {
            server.ClearRequests();

            bool receivedPresenceMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = PubnubCommon.SecretKey,
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);

            ManualResetEvent presenceManualEvent = new ManualResetEvent(false);

            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(p));
                    if (p.Event == "join") { receivedPresenceMessage = true; }
                    presenceManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                        presenceManualEvent.Set();
                    }
                });

            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14833694874957031\",\"r\":7},\"m\":[{\"a\":\"4\",\"f\":512,\"p\":{\"t\":\"14833694873794045\",\"r\":2},\"k\":\"demo-36\",\"c\":\"hello_my_channel-pnpres\",\"d\":{\"action\": \"join\", \"timestamp\": 1483369487, \"uuid\": \"mylocalmachine.mydomain.com\", \"occupancy\": 1},\"b\":\"hello_my_channel-pnpres\"}]}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1},{2}/0", PubnubCommon.SubscribeKey, channel, channel + "-pnpres"))
                    .WithParameter("heartbeat", "300")
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("tt", "0")
                    .WithParameter("uuid", config.Uuid)
                    .WithParameter("signature", "_JVs4gooSMhdgxRO6FNkk6HwlkyxqcRATHU5j3vkJ9s=")
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            expected = "{}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Subscribe<string>().Channels(new [] { channel }).WithPresence().Execute();
            presenceManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
            else Thread.Sleep(100);

            pubnub.Unsubscribe<string>().Channels(new [] { channel }).Execute();

            if (!PubnubCommon.EnableStubTest) { Thread.Sleep(1000); }
            else { Thread.Sleep(100); }

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedPresenceMessage, "Presence message not received");
        }

        [Test]
        public static void ThenPresenceShouldReturnReceivedMessageSSL()
        {
            server.ClearRequests();

            bool receivedPresenceMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = PubnubCommon.SecretKey,
                Uuid = "mytestuuid",
                Secure = true
            };
            server.RunOnHttps(true);

            ManualResetEvent presenceManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(p));
                    if (p.Event == "join") { receivedPresenceMessage = true; }
                    presenceManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                        presenceManualEvent.Set();
                    }
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }


            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14833694874957031\",\"r\":7},\"m\":[{\"a\":\"4\",\"f\":512,\"p\":{\"t\":\"14833694873794045\",\"r\":2},\"k\":\"demo-36\",\"c\":\"hello_my_channel-pnpres\",\"d\":{\"action\": \"join\", \"timestamp\": 1483369487, \"uuid\": \"mylocalmachine.mydomain.com\", \"occupancy\": 1},\"b\":\"hello_my_channel-pnpres\"}]}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1},{2}/0", PubnubCommon.SubscribeKey, channel, channel + "-pnpres"))
                    .WithParameter("heartbeat", "300")
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("tt", "0")
                    .WithParameter("uuid", config.Uuid)
                    .WithParameter("signature", "_JVs4gooSMhdgxRO6FNkk6HwlkyxqcRATHU5j3vkJ9s=")
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            expected = "{}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Subscribe<string>().Channels(new [] { channel }).WithPresence().Execute();
            presenceManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!PubnubCommon.EnableStubTest) { Thread.Sleep(1000); }
            else { Thread.Sleep(100); }

            pubnub.Unsubscribe<string>().Channels(new [] { channel }).Execute();

            Thread.Sleep(100);

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedPresenceMessage, "Presence message not received");
        }

        [Test]
        public static void ThenPresenceShouldReturnCustomUUID()
        {
            server.ClearRequests();

            string customUUID = "mylocalmachine.mydomain.com";
            bool receivedCustomUUID = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = PubnubCommon.SecretKey,
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);

            ManualResetEvent presenceManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(p));
                    receivedCustomUUID = true;
                    presenceManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                        presenceManualEvent.Set();
                    }
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            pubnub.ChangeUUID(customUUID);

            string expected = "{\"t\":{\"t\":\"14833694874957031\",\"r\":7},\"m\":[{\"a\":\"4\",\"f\":512,\"p\":{\"t\":\"14833694873794045\",\"r\":2},\"k\":\"demo-36\",\"c\":\"hello_my_channel-pnpres\",\"d\":{\"action\": \"join\", \"timestamp\": 1483369487, \"uuid\": \"mylocalmachine.mydomain.com\", \"occupancy\": 1},\"b\":\"hello_my_channel-pnpres\"}]}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1},{2}/0", PubnubCommon.SubscribeKey, channel, channel + "-pnpres"))
                    .WithParameter("heartbeat", "300")
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("tt", "0")
                    .WithParameter("uuid", customUUID)
                    .WithParameter("signature", "D7lw9Np5UU_xUTUAe0Sc0L0eSP9aTQljeith_M_rXzI=")
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            expected = "{}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Subscribe<string>().Channels(new [] { channel }).WithPresence().Execute();
            presenceManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!PubnubCommon.EnableStubTest) { Thread.Sleep(1000); }
            else { Thread.Sleep(100); }

            pubnub.Unsubscribe<string>().Channels(new [] { channel }).Execute();

            if (!PubnubCommon.EnableStubTest) { Thread.Sleep(1000); }
            else { Thread.Sleep(100); }

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedCustomUUID, "Custom UUID not received");
        }

        [Test]
        public static void IfHereNowIsCalledThenItShouldReturnInfo()
        {
            server.ClearRequests();

            bool receivedHereNowMessage = false;
            bool receivedErrorMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = "",
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    subscribeManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        receivedErrorMessage = true;
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                    }
                    subscribeManualEvent.Set();
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14828455563482572\",\"r\":7},\"m\":[]}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
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
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Subscribe<string>().Channels(new [] { channel }).Execute();
            subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!receivedErrorMessage)
            {
                if (!PubnubCommon.EnableStubTest) { Thread.Sleep(2000); }
                else Thread.Sleep(200);

                expected = "{\"status\": 200, \"message\": \"OK\", \"service\": \"Presence\", \"uuids\": [\"mytestuuid\"], \"occupancy\": 1}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}", PubnubCommon.SubscribeKey, channel))
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                ManualResetEvent hereNowManualEvent = new ManualResetEvent(false);
                pubnub.HereNow().Channels(new[] { channel }).Async(new PNHereNowResultEx(
                                (r, s) => {
                                    if (r == null) { return; }
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedHereNowMessage = true;
                                    hereNowManualEvent.Set();
                                }));
                hereNowManualEvent.WaitOne(manualResetEventWaitTimeout);

                expected = "{\"status\": 200, \"action\": \"leave\", \"message\": \"OK\", \"service\": \"Presence\"}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channel))
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                pubnub.Unsubscribe<string>().Channels(new[] { channel }).Execute();

                if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
                else Thread.Sleep(100);
            }


            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedHereNowMessage, "here_now message not received");
        }

        [Test]
        public static void IfHereNowIsCalledThenItShouldReturnInfoCipher()
        {
            server.ClearRequests();

            bool receivedHereNowMessage = false;
            bool receivedErrorMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = "",
                CipherKey = "enigma",
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    subscribeManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        receivedErrorMessage = true;
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                    }
                    subscribeManualEvent.Set();
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14828455563482572\",\"r\":7},\"m\":[]}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
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
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Subscribe<string>().Channels(new [] { channel }).Execute();
            subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!receivedErrorMessage)
            {
                if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
                else Thread.Sleep(200);

                expected = "{\"TotalChannels\":1,\"TotalOccupancy\":1,\"Channels\":{\"hello_my_channel\":{\"ChannelName\":\"hello_my_channel\",\"Occupancy\":1,\"Occupants\":[{\"Uuid\":\"mytestuuid\",\"State\":null}]}}}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}", PubnubCommon.SubscribeKey, channel))
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                ManualResetEvent hereNowManualEvent = new ManualResetEvent(false);
                pubnub.HereNow().Channels(new[] { channel }).Async(new PNHereNowResultEx(
                                (r, s) => {
                                    if (r == null) { return; }
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedHereNowMessage = true;
                                    hereNowManualEvent.Set();
                                }));
                hereNowManualEvent.WaitOne(manualResetEventWaitTimeout);

                expected = "{\"status\": 200, \"action\": \"leave\", \"message\": \"OK\", \"service\": \"Presence\"}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channel))
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                pubnub.Unsubscribe<string>().Channels(new[] { channel }).Execute();

                if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
                else Thread.Sleep(100);
            }

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedHereNowMessage, "here_now message not received with cipher");
        }

        [Test]
        public static void IfHereNowIsCalledThenItShouldReturnInfoCipherSecret()
        {
            server.ClearRequests();

            bool receivedHereNowMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = PubnubCommon.SecretKey,
                CipherKey = "enigma",
                Uuid = "mytestuuid"
            };
            server.RunOnHttps(true);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    subscribeManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                    }
                    subscribeManualEvent.Set();
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14828455563482572\",\"r\":7},\"m\":[]}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithParameter("heartbeat", "300")
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("tt", "0")
                    .WithParameter("uuid", config.Uuid)
                    .WithParameter("signature", "o3VANfuhvrxfff1jsBMOc6EQ4LCe8LXHGaDh58QBZFA=")
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            expected = "{}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            
            pubnub.Subscribe<string>().Channels(new [] { channel }).Execute();
            subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
            else Thread.Sleep(200);

            expected = "{\"TotalChannels\":1,\"TotalOccupancy\":1,\"Channels\":{\"hello_my_channel\":{\"ChannelName\":\"hello_my_channel\",\"Occupancy\":1,\"Occupants\":[{\"Uuid\":\"mytestuuid\",\"State\":null}]}}}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent hereNowManualEvent = new ManualResetEvent(false);
            pubnub.HereNow().Channels(new [] { channel }).Async(new PNHereNowResultEx(
                                (r, s) => {
                                    if (r == null) { return; }
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedHereNowMessage = true;
                                    hereNowManualEvent.Set();
                                }));
            hereNowManualEvent.WaitOne(manualResetEventWaitTimeout);

            expected = "{\"status\": 200, \"action\": \"leave\", \"message\": \"OK\", \"service\": \"Presence\"}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Unsubscribe<string>().Channels(new [] { channel }).Execute();

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
            else Thread.Sleep(100);

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedHereNowMessage, "here_now message not received with cipher and secret");
        }

        [Test]
        public static void IfHereNowIsCalledThenItShouldReturnInfoCipherSecretSSL()
        {
            server.ClearRequests();

            bool receivedHereNowMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = PubnubCommon.SecretKey,
                CipherKey = "enigma",
                Uuid = "mytestuuid",
                Secure = true
            };
            server.RunOnHttps(true);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    subscribeManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                    }
                    subscribeManualEvent.Set();
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14828455563482572\",\"r\":7},\"m\":[]}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithParameter("heartbeat", "300")
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("tt", "0")
                    .WithParameter("uuid", config.Uuid)
                    .WithParameter("signature", "o3VANfuhvrxfff1jsBMOc6EQ4LCe8LXHGaDh58QBZFA=")
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            expected = "{}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            
            pubnub.Subscribe<string>().Channels(new [] { channel }).Execute();
            subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
            else Thread.Sleep(200);

            expected = "{\"TotalChannels\":1,\"TotalOccupancy\":1,\"Channels\":{\"hello_my_channel\":{\"ChannelName\":\"hello_my_channel\",\"Occupancy\":1,\"Occupants\":[{\"Uuid\":\"mytestuuid\",\"State\":null}]}}}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent hereNowManualEvent = new ManualResetEvent(false);
            pubnub.HereNow().Channels(new [] { channel }).Async(new PNHereNowResultEx(
                                (r, s) => {
                                    if (r == null) { return; }
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedHereNowMessage = true;
                                    hereNowManualEvent.Set();
                                }));
            hereNowManualEvent.WaitOne(manualResetEventWaitTimeout);

            expected = "{\"status\": 200, \"action\": \"leave\", \"message\": \"OK\", \"service\": \"Presence\"}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Unsubscribe<string>().Channels(new [] { channel }).Execute();

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
            else Thread.Sleep(100);

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedHereNowMessage, "here_now message not received with cipher, secret, ssl");
        }

        [Test]
        public static void IfHereNowIsCalledThenItShouldReturnInfoCipherSSL()
        {
            server.ClearRequests();

            bool receivedHereNowMessage = false;
            bool receivedErrorMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = "",
                CipherKey = "enigma",
                Uuid = "mytestuuid",
                Secure = true
            };
            server.RunOnHttps(true);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                            (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                            (o, p) => {
                                subscribeManualEvent.Set();
                            },
                            (o, s) => {
                                Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                                if (s.StatusCode != 200 || s.Error)
                                {
                                    receivedErrorMessage = true;
                                    if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                                }
                                subscribeManualEvent.Set();
                            });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14828455563482572\",\"r\":7},\"m\":[]}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
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
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Subscribe<string>().Channels(new [] { channel }).Execute();
            subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!receivedErrorMessage)
            {
                if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
                else Thread.Sleep(200);

                expected = "{\"TotalChannels\":1,\"TotalOccupancy\":1,\"Channels\":{\"hello_my_channel\":{\"ChannelName\":\"hello_my_channel\",\"Occupancy\":1,\"Occupants\":[{\"Uuid\":\"mytestuuid\",\"State\":null}]}}}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}", PubnubCommon.SubscribeKey, channel))
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                ManualResetEvent hereNowManualEvent = new ManualResetEvent(false);
                pubnub.HereNow().Channels(new[] { channel }).Async(new PNHereNowResultEx(
                                (r, s) => {
                                    if (r == null) { return; }
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedHereNowMessage = true;
                                    hereNowManualEvent.Set();
                                }));
                hereNowManualEvent.WaitOne(manualResetEventWaitTimeout);

                expected = "{\"status\": 200, \"action\": \"leave\", \"message\": \"OK\", \"service\": \"Presence\"}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channel))
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                pubnub.Unsubscribe<string>().Channels(new[] { channel }).Execute();

                if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
                else Thread.Sleep(100);
            }

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedHereNowMessage, "here_now message not received with cipher, ssl");
        }

        [Test]
        public static void IfHereNowIsCalledThenItShouldReturnInfoSecret()
        {
            server.ClearRequests();

            bool receivedHereNowMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = PubnubCommon.SecretKey,
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    subscribeManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                    }
                    subscribeManualEvent.Set();
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14828455563482572\",\"r\":7},\"m\":[]}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithParameter("heartbeat", "300")
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("tt", "0")
                    .WithParameter("uuid", config.Uuid)
                    .WithParameter("signature", "o3VANfuhvrxfff1jsBMOc6EQ4LCe8LXHGaDh58QBZFA=")
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            expected = "{}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            
            pubnub.Subscribe<string>().Channels(new [] { channel }).Execute();
            subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
            else Thread.Sleep(200);

            expected = "{\"TotalChannels\":1,\"TotalOccupancy\":1,\"Channels\":{\"hello_my_channel\":{\"ChannelName\":\"hello_my_channel\",\"Occupancy\":1,\"Occupants\":[{\"Uuid\":\"mytestuuid\",\"State\":null}]}}}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent hereNowManualEvent = new ManualResetEvent(false);
            pubnub.HereNow().Channels(new [] { channel }).Async(new PNHereNowResultEx(
                                (r, s) => {
                                    if (r == null) { return; }
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedHereNowMessage = true;
                                    hereNowManualEvent.Set();
                                }));
            hereNowManualEvent.WaitOne(manualResetEventWaitTimeout);

            expected = "{\"status\": 200, \"action\": \"leave\", \"message\": \"OK\", \"service\": \"Presence\"}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Unsubscribe<string>().Channels(new [] { channel }).Execute();

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
            else Thread.Sleep(100);

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedHereNowMessage, "here_now message not received with secret key");
        }

        [Test]
        public static void IfHereNowIsCalledThenItShouldReturnInfoSecretSSL()
        {
            server.ClearRequests();

            bool receivedHereNowMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = PubnubCommon.SecretKey,
                Uuid = "mytestuuid",
                Secure = true
            };
            server.RunOnHttps(true);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    subscribeManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                    }
                    subscribeManualEvent.Set();
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14828455563482572\",\"r\":7},\"m\":[]}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithParameter("heartbeat", "300")
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("tt", "0")
                    .WithParameter("uuid", config.Uuid)
                    .WithParameter("signature", "o3VANfuhvrxfff1jsBMOc6EQ4LCe8LXHGaDh58QBZFA=")
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            expected = "{}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            
            pubnub.Subscribe<string>().Channels(new [] { channel }).Execute();
            subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
            else Thread.Sleep(200);

            expected = "{\"TotalChannels\":1,\"TotalOccupancy\":1,\"Channels\":{\"hello_my_channel\":{\"ChannelName\":\"hello_my_channel\",\"Occupancy\":1,\"Occupants\":[{\"Uuid\":\"mytestuuid\",\"State\":null}]}}}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent hereNowManualEvent = new ManualResetEvent(false);
            pubnub.HereNow().Channels(new [] { channel }).Async(new PNHereNowResultEx(
                                (r, s) => {
                                    if (r == null) { return; }
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedHereNowMessage = true;
                                    hereNowManualEvent.Set();
                                }));
            hereNowManualEvent.WaitOne(manualResetEventWaitTimeout);

            expected = "{\"status\": 200, \"action\": \"leave\", \"message\": \"OK\", \"service\": \"Presence\"}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Unsubscribe<string>().Channels(new [] { channel }).Execute();

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
            else Thread.Sleep(100);

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedHereNowMessage, "here_now message not received ,with secret key, ssl");
        }

        [Test]
        public static void IfHereNowIsCalledThenItShouldReturnInfoSSL()
        {
            server.ClearRequests();

            bool receivedHereNowMessage = false;
            bool receivedErrorMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = "",
                Uuid = "mytestuuid",
                Secure = true
            };
            server.RunOnHttps(true);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    subscribeManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        receivedErrorMessage = true;
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                    }
                    subscribeManualEvent.Set();
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14828455563482572\",\"r\":7},\"m\":[]}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
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
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            
            pubnub.Subscribe<string>().Channels(new [] { channel }).Execute();
            subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!receivedErrorMessage)
            {
                if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
                else Thread.Sleep(200);

                expected = "{\"TotalChannels\":1,\"TotalOccupancy\":1,\"Channels\":{\"hello_my_channel\":{\"ChannelName\":\"hello_my_channel\",\"Occupancy\":1,\"Occupants\":[{\"Uuid\":\"mytestuuid\",\"State\":null}]}}}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}", PubnubCommon.SubscribeKey, channel))
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                ManualResetEvent hereNowManualEvent = new ManualResetEvent(false);
                pubnub.HereNow().Channels(new[] { channel }).Async(new PNHereNowResultEx(
                                (r, s) => {
                                    if (r == null) { return; }
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedHereNowMessage = true;
                                    hereNowManualEvent.Set();
                                }));
                hereNowManualEvent.WaitOne(manualResetEventWaitTimeout);

                expected = "{\"status\": 200, \"action\": \"leave\", \"message\": \"OK\", \"service\": \"Presence\"}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channel))
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                pubnub.Unsubscribe<string>().Channels(new[] { channel }).Execute();

                if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
                else Thread.Sleep(100);
            }

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedHereNowMessage, "here_now message not received with ssl");
        }

        [Test]
        public static void IfHereNowIsCalledThenItShouldReturnInfoWithUserState()
        {
            server.ClearRequests();

            bool receivedHereNowMessage = false;
            bool receivedErrorMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    subscribeManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        receivedErrorMessage = true;
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                    }
                    subscribeManualEvent.Set();
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14828455563482572\",\"r\":7},\"m\":[]}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
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
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            
            pubnub.Subscribe<string>().Channels(new [] { channel }).Execute();
            subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!receivedErrorMessage)
            {
                if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
                else Thread.Sleep(200);

                
                Dictionary<string, object> dicState = new Dictionary<string, object>();
                dicState.Add("testkey", "testval");

                expected = "{\"status\": 200, \"message\": \"OK\", \"payload\": {\"testkey\": \"testval\"}, \"service\": \"Presence\"}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/uuid/{2}/data", PubnubCommon.SubscribeKey, channel, config.Uuid))
                        .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                        .WithParameter("requestid", "myRequestId")
                        .WithParameter("state", "%7B%22testkey%22%3A%22testval%22%7D")
                        .WithParameter("timestamp", "1356998400")
                        .WithParameter("uuid", config.Uuid)
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                ManualResetEvent userStateManualEvent = new ManualResetEvent(false);
                pubnub.SetPresenceState()
                                .Channels(new[] { channel })
                                .State(dicState)
                                .Async(new PNSetStateResultExt(
                                (r, s) => {
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    userStateManualEvent.Set();
                                }));
                userStateManualEvent.WaitOne(manualResetEventWaitTimeout);

                expected = "{\"status\": 200, \"message\": \"OK\", \"service\": \"Presence\", \"uuids\": [{\"state\": {\"testkey\": \"testval\"}, \"uuid\": \"mytestuuid\"}], \"occupancy\": 1}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}", PubnubCommon.SubscribeKey, channel))
                        .WithParameter("disable_uuids", "0")
                        .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                        .WithParameter("requestid", "myRequestId")
                        .WithParameter("state", "1")
                        .WithParameter("timestamp", "1356998400")
                        .WithParameter("uuid", config.Uuid)
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                ManualResetEvent hereNowManualEvent = new ManualResetEvent(false);
                pubnub.HereNow().Channels(new[] { channel })
                        .IncludeState(true)
                        .IncludeUUIDs(true)
                        .Async(new PNHereNowResultEx(
                                (r, s) => {
                                    if (r == null) { return; }
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedHereNowMessage = true;
                                    hereNowManualEvent.Set();
                                }));
                hereNowManualEvent.WaitOne(manualResetEventWaitTimeout);

                expected = "{\"status\": 200, \"action\": \"leave\", \"message\": \"OK\", \"service\": \"Presence\"}";

                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channel))
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                pubnub.Unsubscribe<string>().Channels(new[] { channel }).Execute();

                if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
                else Thread.Sleep(100);
            }

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedHereNowMessage, "here_now message not received with user state");
        }

        [Test]
        public static void IfGlobalHereNowIsCalledThenItShouldReturnInfo()
        {
            server.ClearRequests();

            bool receivedHereNowMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                Uuid = "mytestuuid",
                Secure = false
            };

            if (PubnubCommon.PAMEnabled)
            {
                config.SecretKey = PubnubCommon.SecretKey;
            }

            server.RunOnHttps(false);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    subscribeManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                    }
                    subscribeManualEvent.Set();
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14827658395446362\",\"r\":7},\"m\":[]}";
            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithParameter("heartbeat", "300")
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("tt", "0")
                    .WithParameter("uuid", config.Uuid)
                    .WithParameter("signature", "o3VANfuhvrxfff1jsBMOc6EQ4LCe8LXHGaDh58QBZFA=")
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
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
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            
            pubnub.Subscribe<string>().Channels(new [] { channel }).Execute();
            subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
            else Thread.Sleep(200);

            expected = "{\"status\": 200, \"message\": \"OK\", \"payload\": {\"channels\": {}, \"total_channels\": 0, \"total_occupancy\": 0}, \"service\": \"Presence\"}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}", PubnubCommon.SubscribeKey))
                    .WithParameter("disable_uuids", "0")
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("state", "0")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("uuid", config.Uuid)
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent hereNowManualEvent = new ManualResetEvent(false);
            pubnub.HereNow().Async(new PNHereNowResultEx(
                                (r, s) => {
                                    if (r == null) { return; }
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedHereNowMessage = true;
                                    hereNowManualEvent.Set();
                                }));

            hereNowManualEvent.WaitOne(manualResetEventWaitTimeout);

            expected = "{\"status\": 200, \"action\": \"leave\", \"message\": \"OK\", \"service\": \"Presence\"}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Unsubscribe<string>().Channels(new [] { channel }).Execute();

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
            else Thread.Sleep(100);

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedHereNowMessage, "global_here_now message not received");
        }

        [Test]
        public static void IfGlobalHereNowIsCalledThenItShouldReturnInfoWithUserState()
        {
            server.ClearRequests();

            bool receivedHereNowMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    subscribeManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                    }
                    subscribeManualEvent.Set();
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";

            string expected = "{\"t\":{\"t\":\"14827658395446362\",\"r\":7},\"m\":[]}";
            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
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
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            manualResetEventWaitTimeout = (PubnubCommon.EnableStubTest ? 2000 : 310 * 1000);

            
            pubnub.Subscribe<string>().Channels(new [] { channel }).Execute();
            subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);

            
            Dictionary<string, object> dicState = new Dictionary<string, object>();
            dicState.Add("testkey", "testval");

            expected = "[[],\"14740704540745015\"]";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/uuid/{2}/data", PubnubCommon.SubscribeKey, channel, config.Uuid))
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("state", "%7B%22testkey%22%3A%22testval%22%7D")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("uuid", config.Uuid)
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent userStateManualEvent = new ManualResetEvent(false);
            pubnub.SetPresenceState()
                            .Channels(new [] { channel })
                            .State(dicState)
                            .Async(new PNSetStateResultExt(
                                (r, s) => {
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    userStateManualEvent.Set();
                                }));
            userStateManualEvent.WaitOne(manualResetEventWaitTimeout);


            expected = "{\"status\": 200, \"message\": \"OK\", \"payload\": {\"channels\": {\"bot_object\": {\"uuids\": [{\"uuid\": \"0ccff0c1-aa81-421b-8c2b-08a59bd5138c\"}], \"occupancy\": 1}, \"hello_my_channel\": {\"uuids\": [{\"state\": {\"testkey\": \"testval\"}, \"uuid\": \"mytestuuid\"}], \"occupancy\": 1}}, \"total_channels\": 2, \"total_occupancy\": 2}, \"service\": \"Presence\"}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}", PubnubCommon.SubscribeKey))
                    .WithParameter("disable_uuids", "0")
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("state", "1")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("uuid", config.Uuid)
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent hereNowManualEvent = new ManualResetEvent(false);
            pubnub.HereNow()
                    .IncludeState(true)
                    .IncludeUUIDs(true)
                    .Async(new PNHereNowResultEx(
                                (r, s) => {
                                    if (r == null) { return; }
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedHereNowMessage = true;
                                    hereNowManualEvent.Set();
                                }));
            hereNowManualEvent.WaitOne(manualResetEventWaitTimeout);

            expected = "[[],\"14740704540745015\"]";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Unsubscribe<string>().Channels(new [] { channel }).Execute();

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
            else Thread.Sleep(100);

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedHereNowMessage, "global_here_now message not received for user state");
        }

        [Test]
        public static void IfWhereNowIsCalledThenItShouldReturnInfo()
        {
            server.ClearRequests();

            bool receivedWhereNowMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);

            ManualResetEvent subscribeManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    subscribeManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                    }
                    subscribeManualEvent.Set();
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }

            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14827658395446362\",\"r\":7},\"m\":[]}";
            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
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
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            
            pubnub.Subscribe<string>().Channels(new [] { channel }).Execute();
            subscribeManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
            else Thread.Sleep(200);

            expected = "{\"status\": 200, \"message\": \"OK\", \"payload\": {\"channels\": {}, \"total_channels\": 0, \"total_occupancy\": 0}, \"service\": \"Presence\"}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/uuid/{1}", PubnubCommon.SubscribeKey, config.Uuid))
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("uuid", config.Uuid)
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent whereNowManualEvent = new ManualResetEvent(false);
            pubnub.WhereNow().Uuid(config.Uuid).Async(new PNWhereNowResultExt(
                                (r, s) => {
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedWhereNowMessage = true;
                                    whereNowManualEvent.Set();
                                }));
            whereNowManualEvent.WaitOne();

            expected = "[[],\"14740704540745015\"]";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/leave", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Unsubscribe<string>().Channels(new [] { channel }).Execute();

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(1000);
            else Thread.Sleep(100);

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedWhereNowMessage, "where_now message not received");
        }

        [Test]
        public static void IfSetAndGetUserStateThenItShouldReturnInfo()
        {
            server.ClearRequests();

            string customUUID = "mylocalmachine.mydomain.com";
            bool receivedUserStateMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);

            pubnub = createPubNubInstance(config);
            pubnub.ChangeUUID(customUUID);

            manualResetEventWaitTimeout = 310 * 1000;
            string channel = "hello_my_channel";

            Dictionary<string, object> dicState = new Dictionary<string, object>();
            dicState.Add("testkey", "testval");

            string expected = "{\"status\": 200, \"message\": \"OK\", \"payload\": {\"testkey\": \"testval\"}, \"service\": \"Presence\"}";
            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/uuid/{2}/data", PubnubCommon.SubscribeKey, channel, customUUID))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent userStateManualEvent = new ManualResetEvent(false);
            pubnub.SetPresenceState()
                            .Channels(new [] { channel })
                            .State(dicState)
                            .Async(new PNSetStateResultExt(
                                (r, s) => {
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedUserStateMessage = true;
                                    userStateManualEvent.Set();
                                }));

            userStateManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (receivedUserStateMessage)
            {
                if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
                else Thread.Sleep(200);

                receivedUserStateMessage = false;
                userStateManualEvent = new ManualResetEvent(false);

                expected = "{\"status\": 200, \"uuid\": \"mylocalmachine.mydomain.com\", \"service\": \"Presence\", \"message\": \"OK\", \"payload\": {\"testkey\": \"testval\"}, \"channel\": \"hello_my_channel\"}";
                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/uuid/{2}", PubnubCommon.SubscribeKey, channel, customUUID))
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                pubnub.GetPresenceState()
                                .Channels(new [] { channel })
                                .Async(new PNGetStateResultExt(
                                (r, s) => {
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedUserStateMessage = true;
                                    userStateManualEvent.Set();
                                }));
                userStateManualEvent.WaitOne(manualResetEventWaitTimeout);
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedUserStateMessage, "IfSetAndGetUserStateThenItShouldReturnInfo failed");
        }

        [Test]
        public static void IfSetAndDeleteUserStateThenItShouldReturnInfo()
        {
            server.ClearRequests();

            Request getRequest = new Request();
            bool receivedUserStateMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);

            pubnub = createPubNubInstance(config);

            manualResetEventWaitTimeout = 310 * 1000;
            string channel = "hello_my_channel";

            
            Dictionary<string, object> dicState = new Dictionary<string, object>();
            dicState.Add("k", "v");

            string expected = "{\"status\": 200, \"message\": \"OK\", \"payload\": {\"k\": \"v\"}, \"service\": \"Presence\"}";
            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/uuid/{2}/data", PubnubCommon.SubscribeKey, channel, config.Uuid))
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("state", "%7B%22k%22%3A%22v%22%7D")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("uuid", config.Uuid)
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            ManualResetEvent userStateManualEvent = new ManualResetEvent(false);
            pubnub.SetPresenceState()
                            .Channels(new [] { channel })
                            .State(dicState)
                            .Async(new PNSetStateResultExt(
                                (r, s) => {
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedUserStateMessage = true;
                                    userStateManualEvent.Set();
                                }));

            userStateManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
            else Thread.Sleep(200);

            if (receivedUserStateMessage)
            {
                expected = "{\"status\": 200, \"uuid\": \"mytestuuid\", \"service\": \"Presence\", \"message\": \"OK\", \"payload\": {\"k\": \"v\"}, \"channel\": \"hello_my_channel\"}";
                getRequest = new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/uuid/{2}", PubnubCommon.SubscribeKey, channel, config.Uuid))
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("uuid", config.Uuid)
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK);
                server.AddRequest(getRequest);

                receivedUserStateMessage = false;
                userStateManualEvent = new ManualResetEvent(false);
                pubnub.GetPresenceState()
                                .Channels(new [] { channel })
                                .Async(new PNGetStateResultExt(
                                (r, s) => {
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedUserStateMessage = true;
                                    userStateManualEvent.Set();
                                }));

                userStateManualEvent.WaitOne(manualResetEventWaitTimeout);

                if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
                else Thread.Sleep(200);
            }

            if (receivedUserStateMessage)
            {
                receivedUserStateMessage = false;

                userStateManualEvent = new ManualResetEvent(false);
                dicState = new Dictionary<string, object>();
                dicState.Add("k", null);

                expected = "{\"status\": 200, \"message\": \"OK\", \"payload\": {\"k\": null}, \"service\": \"Presence\"}";
                server.AddRequest(new Request()
                        .WithMethod("GET")
                        .WithPath(String.Format("/v2/presence/sub_key/{0}/channel/{1}/uuid/{2}/data", PubnubCommon.SubscribeKey, channel, config.Uuid))
                        .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                        .WithParameter("requestid", "myRequestId")
                        .WithParameter("state", "%7B%22k%22%3Anull%7D")
                        .WithParameter("timestamp", "1356998400")
                        .WithParameter("uuid", config.Uuid)
                        .WithResponse(expected)
                        .WithStatusCode(System.Net.HttpStatusCode.OK));

                pubnub.SetPresenceState()
                                .Channels(new [] { channel })
                                .State(dicState)
                                .Async(new PNSetStateResultExt(
                                (r, s) => {
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedUserStateMessage = true;
                                    userStateManualEvent.Set();
                                }));

                userStateManualEvent.WaitOne(manualResetEventWaitTimeout);

                if (!PubnubCommon.EnableStubTest) Thread.Sleep(2000);
                else Thread.Sleep(200);
            }

            if (receivedUserStateMessage)
            {
                receivedUserStateMessage = false;

                expected = "{\"status\": 200, \"uuid\": \"mytestuuid\", \"service\": \"Presence\", \"message\": \"OK\", \"payload\": {\"k\": null}, \"channel\": \"hello_my_channel\"}";
                getRequest.WithResponse(expected);
                server.AddRequest(getRequest);

                userStateManualEvent = new ManualResetEvent(false);
                pubnub.GetPresenceState()
                                .Channels(new [] { channel })
                                .Async(new PNGetStateResultExt(
                                (r, s) => {
                                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(r));
                                    receivedUserStateMessage = true;
                                    userStateManualEvent.Set();
                                }));

                userStateManualEvent.WaitOne(manualResetEventWaitTimeout);
            }

            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedUserStateMessage, "IfSetAndDeleteUserStateThenItShouldReturnInfo message not received");
        }

        [Test]
        public static void ThenPresenceHeartbeatShouldReturnMessage()
        {
            server.ClearRequests();

            bool receivedPresenceMessage = false;

            PNConfiguration config = new PNConfiguration
            {
                PublishKey = PubnubCommon.PublishKey,
                SubscribeKey = PubnubCommon.SubscribeKey,
                SecretKey = PubnubCommon.SecretKey,
                Uuid = "mytestuuid",
                Secure = false
            };
            server.RunOnHttps(false);


            ManualResetEvent presenceManualEvent = new ManualResetEvent(false);
            SubscribeCallback listenerSubCallack = new SubscribeCallbackExt(
                (o, m) => { Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(m)); },
                (o, p) => {
                    Console.WriteLine(pubnub.JsonPluggableLibrary.SerializeToJsonString(p));
                    if (p.Event == "join") { receivedPresenceMessage = true; }
                    presenceManualEvent.Set();
                },
                (o, s) => {
                    Console.WriteLine("{0} {1} {2}", s.Operation, s.Category, s.StatusCode);
                    if (s.StatusCode != 200 || s.Error)
                    {
                        if (s.ErrorData != null) { Console.WriteLine(s.ErrorData.Information); }
                        presenceManualEvent.Set();
                    }
                });
            pubnub = createPubNubInstance(config);
            if (!pubnub.AddListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: AddListener failed");
            }


            string channel = "hello_my_channel";
            manualResetEventWaitTimeout = 310 * 1000;

            string expected = "{\"t\":{\"t\":\"14828440156769626\",\"r\":7},\"m\":[{\"a\":\"4\",\"f\":512,\"p\":{\"t\":\"14828440155770431\",\"r\":2},\"k\":\"demo-36\",\"c\":\"hello_my_channel-pnpres\",\"d\":{\"action\": \"join\", \"timestamp\": 1482844015, \"uuid\": \"mytestuuid\", \"occupancy\": 1},\"b\":\"hello_my_channel-pnpres\"}]}";
            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel + "," + channel + "-pnpres"))
                    .WithParameter("heartbeat", "300")
                    .WithParameter("pnsdk", PubnubCommon.EncodedSDK)
                    .WithParameter("requestid", "myRequestId")
                    .WithParameter("timestamp", "1356998400")
                    .WithParameter("tt", "0")
                    .WithParameter("uuid", config.Uuid)
                    .WithParameter("signature", "_JVs4gooSMhdgxRO6FNkk6HwlkyxqcRATHU5j3vkJ9s=")
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            expected = "{}";

            server.AddRequest(new Request()
                    .WithMethod("GET")
                    .WithPath(String.Format("/v2/subscribe/{0}/{1}/0", PubnubCommon.SubscribeKey, channel))
                    .WithResponse(expected)
                    .WithStatusCode(System.Net.HttpStatusCode.OK));

            pubnub.Subscribe<string>().Channels(new [] { channel }).WithPresence().Execute();
            presenceManualEvent.WaitOne(manualResetEventWaitTimeout);

            if (!PubnubCommon.EnableStubTest) { Thread.Sleep(pubnub.PNConfig.PresenceTimeout + (3 * 1000)); }

            pubnub.Unsubscribe<string>().Channels(new [] { channel }).Execute();

            if (!PubnubCommon.EnableStubTest) { Thread.Sleep(1000); }
            else { Thread.Sleep(100); }

            if (!pubnub.RemoveListener(listenerSubCallack))
            {
                System.Diagnostics.Debug.WriteLine("ATTENTION: RemoveListener failed");
            }
            pubnub.Destroy();
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedPresenceMessage, "ThenPresenceHeartbeatShouldReturnMessage not received");
        }
        
    }
}
