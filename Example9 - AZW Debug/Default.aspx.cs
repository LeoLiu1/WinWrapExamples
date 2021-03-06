﻿using Examples.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Web.UI;

//------------------------------------------------------------------------------
// <copyright from='2013' to='2014' company='Polar Engineering and Consulting'>
//    Copyright (c) Polar Engineering and Consulting. All Rights Reserved.
//
//    This file contains confidential material.
//
// </copyright>
//------------------------------------------------------------------------------

// how to debug an azure website
// http://jessekallhoff.com/2014/07/09/remote-debugging-with-windows-azure/

namespace Example
{
    public partial class Form1 : Page, IHost
    {
        private ApplicationQueue commands_;
        private Dictionary<int, ApplicationQueue> responses_;

        private static readonly string[] scripts_ =
        {
            "DOTNet.bas",
            "BlockedParseError.bas",
            "BlockedRuntimeError.bas",
            "ParseError.bas",
            "RuntimeError.bas",
            "Stop.bas",
            "TooLong.bas"
        };
        private bool timedout_;
        private DateTime timelimit_;

        protected void Page_Load(object sender, EventArgs e)
        {
            ScriptingLanguage.SetHost(this);
            if (!Page.IsPostBack)
            {
                Session["Text"] = "";
                LabelTarget.Text = new Random().Next(int.MaxValue).ToString();
                ListBoxScripts_Initialize();
            }
        }

        protected void ButtonRun_Click(object sender, EventArgs e)
        {
            WinWrapExecute(false);
        }

        protected void ButtonDebug_Click(object sender, EventArgs e)
        {
            //responses_.Append("Debug");
            //Thread.Sleep(1000);
            commands_ = ApplicationQueue.Create("commands", LabelTarget.Text);
            responses_ = new Dictionary<int, ApplicationQueue>();
            WinWrapExecute(true);
            foreach (ApplicationQueue response in responses_.Values)
                response.Delete();
        }

        protected void ButtonShow_Click(object sender, EventArgs e)
        {
            Log("");
        }

        private void WinWrapExecute(bool debug)
        {
            TheIncident = new Incident();
            using (var basicNoUIObj = new WinWrap.Basic.BasicNoUIObj())
            {
                basicNoUIObj.Begin += basicNoUIObj_Begin;
                basicNoUIObj.DoEvents += basicNoUIObj_DoEvents;
                basicNoUIObj.End += basicNoUIObj_End;
                basicNoUIObj.ErrorAlert += basicNoUIObj_ErrorAlert;
                basicNoUIObj.Pause_ += basicNoUIObj_Pause_;
                basicNoUIObj.Resume += basicNoUIObj_Resume;
                basicNoUIObj.Synchronizing += basicNoUIObj_Synchronizing;
                basicNoUIObj.Secret = new Guid(AzureOnlyStrings.GetNamedString("Guid", "00000000-0000-0000-0000-000000000000"));
                basicNoUIObj.Initialize();
                basicNoUIObj.AddScriptableObjectModel(typeof(ScriptingLanguage));

                if (debug)
                {
                    // prepare for debugging
                    basicNoUIObj.Synchronized = true;
                    Log("Debugging...");
                }

                try
                {
                    if (!basicNoUIObj.LoadModule(ScriptPath("Globals.bas")))
                        throw basicNoUIObj.Error.Exception;

                    using (var module = basicNoUIObj.ModuleInstance(ScriptPath(Script), false))
                    {
                        if (module == null)
                            throw basicNoUIObj.Error.Exception;

                        if (debug)
                        {
                            // step into the script event handler
                            module.StepInto = true;
                            timelimit_ = DateTime.Now + new TimeSpan(0, 0, 30); // timeout in 30 seconds
                        }

                        // Execute script code via an event
                        ScriptingLanguage.TheIncident.Start("Default.aspx");
                    }
                }
                catch (Exception ex)
                {
                    if (debug)
                    {
                        // report error and allow remote to catch up
                        basicNoUIObj.ReportError(ex);
                        basicNoUIObj.Wait(3);
                    }

                    basicNoUIObj.ReportError(ex);
                }

                if (debug)
                {
                    basicNoUIObj.Wait(3);
                    Log("Debugging complete.");
                }
            }
            TheIncident = null;
        }

        private void ProcessSynchronizingEvents(WinWrap.Basic.BasicNoUIObj basicNoUIObj)
        {
            string commands = commands_.ReadAll();
            if (!string.IsNullOrEmpty(commands))
            {
                timelimit_ = DateTime.Now + new TimeSpan(0, 0, 10); // timeout in ten seconds
                string[] commands2 = commands.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string command in commands2)
                {
                    string[] parts = command.Split(new char[] { ' ' }, 2);
                    int id = int.Parse(parts[0]);
                    string param = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                    basicNoUIObj.Synchronize(param, id);
                }
            }
        }

        void basicNoUIObj_DoEvents(object sender, EventArgs e)
        {
            WinWrap.Basic.BasicNoUIObj basicNoUIObj = sender as WinWrap.Basic.BasicNoUIObj;
            if (basicNoUIObj.Synchronized)
            {
                // process pending debugging commands
                ProcessSynchronizingEvents(basicNoUIObj);
            }

            if (basicNoUIObj.Run && DateTime.Now >= timelimit_)
            {
                timedout_ = true;
                // time timelimit has been reached, terminate the script
                basicNoUIObj.Run = false;
            }
        }

        void basicNoUIObj_Begin(object sender, EventArgs e)
        {
            WinWrap.Basic.BasicNoUIObj basicNoUIObj = sender as WinWrap.Basic.BasicNoUIObj;
            timedout_ = false;
            timelimit_ = DateTime.Now + new TimeSpan(0, 0, 1); // timeout in one second
        }

        void basicNoUIObj_End(object sender, EventArgs e)
        {
            WinWrap.Basic.BasicNoUIObj basicNoUIObj = sender as WinWrap.Basic.BasicNoUIObj;
            if (timedout_ && basicNoUIObj.Error == null)
            {
                // timedout
                LogError(Examples.SharedSource.WinWrapBasic.FormatTimeoutError(basicNoUIObj, timedout_));
            }
        }

        void basicNoUIObj_Pause_(object sender, EventArgs e)
        {
            WinWrap.Basic.BasicNoUIObj basicNoUIObj = sender as WinWrap.Basic.BasicNoUIObj;
            // timedout or paused while not debugging
            if (timedout_ || !basicNoUIObj.Synchronized)
            {
                if (basicNoUIObj.Error == null)
                {
                    LogError(Examples.SharedSource.WinWrapBasic.FormatTimeoutError(basicNoUIObj, timedout_));
                }

                // Script execution has paused, terminate the script
                basicNoUIObj.Run = false;
            }
        }

        void basicNoUIObj_Resume(object sender, EventArgs e)
        {
        }

        void basicNoUIObj_ErrorAlert(object sender, EventArgs e)
        {
            WinWrap.Basic.BasicNoUIObj basicNoUIObj = sender as WinWrap.Basic.BasicNoUIObj;
            LogError(basicNoUIObj.Error);
        }

        void basicNoUIObj_Synchronizing(object sender, WinWrap.Basic.Classic.SynchronizingEventArgs e)
        {
            string data = Convert.ToBase64String(Encoding.UTF8.GetBytes(e.Param)) + "\r\n";
            if (e.Id >= 0)
            {
                // response for a specific remote
                ApplicationQueue response = null;
                if (!responses_.TryGetValue(e.Id, out response))
                {
                    response = ApplicationQueue.Create("responses", LabelTarget.Text, e.Id.ToString());
                    responses_.Add(e.Id, response);
                }

                response.Append(data);
            }
            else
            {
                // response for all remotes
                foreach (ApplicationQueue response in responses_.Values)
                    response.Append(data);
            }
        }
    }
}