﻿using System;
using System.Threading;

namespace CM3D2.MaidFiddler.Plugin.Gui
{
    public partial class MaidFiddlerGUI
    {
        private bool destroyGUI;

        public void Close(bool exit)
        {
            destroyGUI = exit;
            Close();
        }

        public void InvokeAsync(Delegate method, params object[] args)
        {
            if (!IsHandleCreated)
            {
                Debugger.WriteLine($"Attempted to invoke asynchronously {method.Method.Name} but found no handle! Creating one...");
                CreateHandle();
                InvokeAsync(method, args);
            }

            BeginInvoke(method, args);
        }
    }
}