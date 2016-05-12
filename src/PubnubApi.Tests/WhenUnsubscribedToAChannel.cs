﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.ComponentModel;
using System.Threading;
using System.Collections;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
using PubnubApi;

namespace PubNubMessaging.Tests
{
    [TestFixture]
    public class WhenUnsubscribedToAChannel
    {
        ManualResetEvent meNotSubscribed = new ManualResetEvent(false);
        ManualResetEvent meChannelSubscribed = new ManualResetEvent(false);
        ManualResetEvent meChannelUnsubscribed = new ManualResetEvent(false);
        ManualResetEvent grantManualEvent = new ManualResetEvent(false);

        bool receivedNotSubscribedMessage = false;
        bool receivedUnsubscribedMessage = false;
        bool receivedChannelConnectedMessage = false;
        bool receivedGrantMessage = false;

        Pubnub pubnub = null;

        [TestFixtureSetUp]
        public void Init()
        {
            if (!PubnubCommon.PAMEnabled) return;

            receivedGrantMessage = false;

            PNConfiguration config = new PNConfiguration();
            config.SubscribeKey = PubnubCommon.SubscribeKey;
            config.PublishKey = PubnubCommon.PublishKey;
            config.SecretKey = PubnubCommon.SecretKey;
            config.CiperKey = "";
            config.Secure = false;

            pubnub = new Pubnub(config);

            PubnubUnitTest unitTest = new PubnubUnitTest();
            unitTest.TestClassName = "GrantRequestUnitTest";
            unitTest.TestCaseName = "Init";
            pubnub.PubnubUnitTest = unitTest;

            string channel = "hello_my_channel";

            pubnub.GrantAccess(channel, true, true, 20, ThenUnsubscribeInitializeShouldReturnGrantMessage, DummyErrorCallback);
            Thread.Sleep(1000);

            grantManualEvent.WaitOne();

            pubnub.EndPendingRequests(); 
            pubnub.PubnubUnitTest = null;
            pubnub = null;
            Assert.IsTrue(receivedGrantMessage, "WhenUnsubscribedToAChannel Grant access failed.");
        }

        [Test]
        public void ThenNoExistChannelShouldReturnNotSubscribed()
        {
            receivedNotSubscribedMessage = false;

            PNConfiguration config = new PNConfiguration();
            config.SubscribeKey = PubnubCommon.SubscribeKey;
            config.PublishKey = PubnubCommon.PublishKey;
            config.SecretKey = "";
            config.CiperKey = "";
            config.Secure = false;

            pubnub = new Pubnub(config);

            PubnubUnitTest unitTest = new PubnubUnitTest();
            unitTest.TestClassName = "WhenUnsubscribedToAChannel";
            unitTest.TestCaseName = "ThenNoExistChannelShouldReturnNotSubscribed";

            pubnub.PubnubUnitTest = unitTest;

            string channel = "hello_my_channel";

            pubnub.Unsubscribe<string>(channel, NoExistChannelErrorCallback);

            meNotSubscribed.WaitOne();

            pubnub.EndPendingRequests(); 
            pubnub.PubnubUnitTest = null;
            pubnub = null;

            Assert.IsTrue(receivedNotSubscribedMessage, "WhenUnsubscribedToAChannel --> ThenNoExistChannelShouldReturnNotSubscribed Failed");
        }

        [Test]
        public void ThenShouldReturnUnsubscribedMessage()
        {
            receivedChannelConnectedMessage = false;
            receivedUnsubscribedMessage = false;

            PNConfiguration config = new PNConfiguration();
            config.SubscribeKey = PubnubCommon.SubscribeKey;
            config.PublishKey = PubnubCommon.PublishKey;
            config.SecretKey = "";
            config.CiperKey = "";
            config.Secure = false;

            pubnub = new Pubnub(config);

            PubnubUnitTest unitTest = new PubnubUnitTest();
            unitTest.TestClassName = "WhenUnsubscribedToAChannel";
            unitTest.TestCaseName = "ThenShouldReturnUnsubscribedMessage";

            pubnub.PubnubUnitTest = unitTest;

            string channel = "hello_my_channel";

            pubnub.Subscribe<string>(channel, DummyMethodChannelSubscribeUserCallback, DummyMethodChannelSubscribeConnectCallback, DummyMethodUnsubscribeChannelDisconnectCallback, DummyErrorCallback);
            meChannelSubscribed.WaitOne();

            if (receivedChannelConnectedMessage)
            {
                pubnub.Unsubscribe<string>(channel, DummyErrorCallback);
                meChannelUnsubscribed.WaitOne();
            }

            pubnub.EndPendingRequests(); 
            pubnub.PubnubUnitTest = null;
            pubnub = null;

            Assert.IsTrue(receivedUnsubscribedMessage, "WhenUnsubscribedToAChannel --> ThenShouldReturnUnsubscribedMessage Failed");
        }

        void ThenUnsubscribeInitializeShouldReturnGrantMessage(GrantAck receivedMessage)
        {
            try
            {
                if (receivedMessage != null)
                {
                    var status = receivedMessage.StatusCode;
                    if (status == 200)
                    {
                        receivedGrantMessage = true;
                    }

                }
            }
            catch { }
            finally
            {
                grantManualEvent.Set();
            }
        }

        private void DummyMethodChannelSubscribeUserCallback(Message<string> result)
        {
        }

        private void DummyMethodChannelSubscribeConnectCallback(ConnectOrDisconnectAck result)
        {
            if (result.StatusMessage.Contains("Connected"))
            {
                receivedChannelConnectedMessage = true;
            }
            meChannelSubscribed.Set();
        }

        private void DummyMethodUnsubscribeChannelUserCallback(string result)
        {
        }

        private void DummyMethodUnsubscribeChannelConnectCallback(ConnectOrDisconnectAck result)
        {
        }

        private void DummyMethodUnsubscribeChannelDisconnectCallback(ConnectOrDisconnectAck result)
        {
            if (result.StatusMessage.Contains("Unsubscribed from"))
            {
                receivedUnsubscribedMessage = true;
            }
            meChannelUnsubscribed.Set();
        }

        private void DummyMethodNoExistChannelUnsubscribeChannelUserCallback(string result)
        {
        }

        private void DummyMethodNoExistChannelUnsubscribeChannelConnectCallback(ConnectOrDisconnectAck result)
        {
        }

        private void DummyMethodNoExistChannelUnsubscribeChannelDisconnectCallback1(ConnectOrDisconnectAck result)
        {
        }

        private void DummyErrorCallback(PubnubClientError result)
        {
        }

        private void NoExistChannelErrorCallback(PubnubClientError result)
        {
            if (result != null && result.Message.ToLower().Contains("not subscribed"))
            {
                receivedNotSubscribedMessage = true;
            }
            meNotSubscribed.Set();
        }
    }
}
