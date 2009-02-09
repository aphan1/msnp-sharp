using System;
using System.Net;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;

namespace MSNPSharp.DataTransfer
{
    using MSNPSharp.IO;
    using MSNPSharp.Core;
    using MSNPSharp.DataTransfer;

    #region P2PApplicationAttribute

    [AttributeUsage(AttributeTargets.Class)]
    public class P2PApplicationAttribute : Attribute
    {
        uint appId;
        string eufGuid;

        public uint AppId
        {
            get
            {
                return appId;
            }
        }

        public string EufGuid
        {
            get
            {
                return eufGuid;
            }
        }

        public P2PApplicationAttribute(uint appID, string eufGuid)
        {
            this.appId = appID;
            this.eufGuid = eufGuid;
        }
    }
    #endregion

    public abstract class P2PBridge
    {
    }





    public static class P2PTransfers
    {
        private static List<P2PSession> sessions = new List<P2PSession>();
        private static List<P2PBridge> bridges = new List<P2PBridge>();
        private static Dictionary<uint, KeyValuePair<P2PMessage, P2PAckHandler>> ackHandlers = new Dictionary<uint, KeyValuePair<P2PMessage, P2PAckHandler>>();



        public static IEnumerable<P2PSession> Sessions
        {
            get
            {
                return sessions;
            }
        }

        public static P2PSession AddTransfer(P2PApplication app)
        {
            P2PSession session = new P2PSession(app);
            session.Closed += SessionClosed;

            lock (sessions)
                sessions.Add(session);

            return session;
        }

        public static P2PSession FindSession(P2PMessage msg)
        {
            uint sessionID = msg.SessionId;

            if (sessionID == 0)
            {
                SLPMessage slp = msg.InnerMessage as SLPMessage;
                if (slp.BodyValues.ContainsKey("SessionID"))
                {
                    if (!uint.TryParse(slp.BodyValues["SessionID"].Value, out sessionID))
                    {
                        Trace.WriteLineIf(Settings.TraceSwitch.TraceWarning, "Unable to parse SLP message SessionID", "P2PTransfers");
                        sessionID = 0;
                    }
                }

                if (sessionID == 0)
                {
                    // We don't get a session ID in BYE requests
                    // so we need to find the session by its call ID
                    foreach (P2PSession session in sessions)
                    {
                        if (session.Invite.CallId == slp.CallId)
                            return session;
                    }
                }
            }

            // Sometimes we only have a message ID to find the session with...
            // e.g. the waiting (flag 4) messages wlm sends sometimes
            if ((sessionID == 0) && (msg.Identifier != 0))
            {
                foreach (P2PSession session in sessions)
                {
                    uint expected = session.RemoteIdentifier + 1;
                    if (expected == session.RemoteBaseIdentifier)
                        expected++;

                    if (msg.Identifier == expected)
                        return session;
                }
            }

            if (sessionID == 0)
                return null;

            foreach (P2PSession session in sessions)
            {
                if (session.SessionId == sessionID)
                    return session;
            }

            return null;
        }

        private static void SessionClosed(object sender, EventArgs args)
        {
            P2PSession session = sender as P2PSession;
            session.Closed -= SessionClosed;

            Trace.WriteLineIf(Settings.TraceSwitch.TraceInfo, String.Format("P2PSession {0} closed, removing", session.SessionId), "P2PTransfers");

            lock (sessions)
                sessions.Remove(session);

            session.Dispose();
        }




        public static void RegisterP2PAckHandler(P2PMessage msg, P2PAckHandler handler)
        {
            ackHandlers[msg.AckIdentifier] = new KeyValuePair<P2PMessage, P2PAckHandler>(msg, handler);
        }
    }

    public static class P2PApplications
    {
        private static List<P2PApp> p2pApps = new List<P2PApp>();
        private struct P2PApp
        {
            public UInt32 AppId;
            public Type AppType;
            public Guid EufGuid;
        }

        static P2PApplications()
        {
            try
            {
                AddApplication(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                Trace.WriteLineIf(Settings.TraceSwitch.TraceError, "Error loading built-in p2p applications: " + e.Message, "P2PApplications");
            }
        }

        #region Add/Find Application

        public static void AddApplication(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(P2PApplicationAttribute), false).Length > 0)
                    AddApplication(type);
            }
        }

        public static void AddApplication(Type type)
        {
            foreach (P2PApplicationAttribute att in type.GetCustomAttributes(typeof(P2PApplicationAttribute), false))
            {
                P2PApp app = new P2PApp();
                app.AppType = type;
                app.AppId = att.AppId;
                app.EufGuid = new Guid(att.EufGuid);

                p2pApps.Add(app);
            }
        }

        internal static Type GetApplication(Guid eufGuid, uint appId)
        {
            if (appId != 0 && eufGuid != Guid.Empty)
            {
                foreach (P2PApp app in p2pApps)
                {
                    if (app.EufGuid == eufGuid && app.AppId == appId)
                        return app.AppType;
                }
            }

            foreach (P2PApp app in p2pApps)
            {
                if (app.EufGuid == eufGuid)
                    return app.AppType;
                else if (app.AppId == appId)
                    return app.AppType;
            }

            return null;
        }

        internal static uint FindApplicationId(P2PApplication p2pApp)
        {
            foreach (P2PApp app in p2pApps)
            {
                if (app.AppType == p2pApp.GetType())
                    return app.AppId;
            }

            return 0;
        }

        internal static Guid FindApplicationEufGuid(P2PApplication p2pApp)
        {
            foreach (P2PApp app in p2pApps)
            {
                if (app.AppType == p2pApp.GetType())
                    return app.EufGuid;
            }

            return Guid.Empty;
        }







       



        #endregion

    }
};