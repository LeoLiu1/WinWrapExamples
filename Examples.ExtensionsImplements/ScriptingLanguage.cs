﻿//------------------------------------------------------------------------------
// <copyright from='2013' to='2014' company='Polar Engineering and Consulting'>
//    Copyright (c) Polar Engineering and Consulting. All Rights Reserved.
//
//    This file contains confidential material.
//
// </copyright>
//------------------------------------------------------------------------------

namespace Examples.Extensions
{
    /// AddScriptableObjectModel(typeof(Extensions))
    /// adds this static class to the scripting language directly
    [Scriptable]
    public static class ScriptingLanguage
    {
        // accessible by all classes in this assembly
        internal static IHost Host { get; private set; }

        // make TheIncident available to WinWrap Basic script
        [Scriptable]
        public static Incident TheIncident { get { return Host.TheIncident; } }

        public static void SetHost(IHost host)
        {
            Host = host;
        }
    }
}
