﻿/*
 * Copyright (c) 2013 Citrix Systems, Inc. All Rights Reserved.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

using Citrix.Diagnostics;

namespace Citrix.SelfServiceDesktops.Agent {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main() {
            CtxTrace.Initialize("self-service-desktops-agent", true);
            ErrorHandler.Initialize();
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
            { 
                new AgentService() 
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
