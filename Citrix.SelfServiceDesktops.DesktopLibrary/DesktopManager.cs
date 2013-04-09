﻿/*
 * Copyright (c) 2013 Citrix Systems, Inc. All Rights Reserved.
 */
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Citrix.Diagnostics;
using Citrix.SelfServiceDesktops.DesktopModel;
using CloudStack.SDK;

namespace Citrix.SelfServiceDesktops.DesktopLibrary {

    using Configuration;

    public class DesktopManager : IDesktopManager {


        public const string DesktopSuffixFormat = "00";

        private IDesktopServiceConfiguration config;
        private Client cloudStackClient;

        internal DesktopManager(string userName, string password) {

            config = DesktopServiceConfiguration.Instance;
            cloudStackClient = new Client(config.CLoudStackUri);
            cloudStackClient.Login(userName, password, true);
        }

        #region IDesktopManager implementation

        public IEnumerable<IDesktopOffering> ListDesktopOfferings() {
            return config.DesktopOfferings.Cast<IDesktopOffering>();
        }

        public IEnumerable<IDesktop> ListDesktops() {
            ListVirtualMachinesRequest request = new ListVirtualMachinesRequest();
            ListVirtualMachinesResponse response = cloudStackClient.ListVirtualMachines(request);
            return FilterDesktops(response.VirtualMachine, config.DesktopOfferings.Cast<IDesktopOffering>()); 
            
        }

        public IDesktop CreateDesktop(string desktopOfferingName) {
            IDesktopOffering offering = ListDesktopOfferings().First(o => (o.Name == desktopOfferingName));
            string name = GetNextDesktopName(offering);
            DeployVirtualMachineRequest request = new DeployVirtualMachineRequest() {
                ServiceOfferingId = offering.ServiceOfferingId,
                TemplateId = offering.TemplateId,
                ZoneId = offering.ZoneId,
                DisplayName = name
            };
            request.Parameters["name"] = name;
            request.WithNetworkIds(offering.NetworkId);
            string id = cloudStackClient.DeployVirtualMachine(request);
            return new Desktop(id, name, null, DesktopState.Creating);
        }

        public void DestroyDesktop(string desktopId) {
            APIRequest request = new APIRequest("destroyVirtualMachine");
            request.Parameters["id"] = desktopId;
            cloudStackClient.SendRequest(request);
        }

        public void StartDesktop(string desktopId) {
            APIRequest request = new APIRequest("startVirtualMachine");
            request.Parameters["id"] = desktopId;
            cloudStackClient.SendRequest(request);
        }

        public void StopDesktop(string desktopId) {
            APIRequest request = new APIRequest("stopVirtualMachine");
            request.Parameters["id"] = desktopId;
            cloudStackClient.SendRequest(request);
        }

        public void RestartDesktop(string desktopId) {
            APIRequest request = new APIRequest("rebootVirtualMachine");
            request.Parameters["id"] = desktopId;
            cloudStackClient.SendRequest(request);
        }

        public Uri BrokerUrl {
            get { return config.BrokerUri; }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Filters the list of virtual machines that may be self-service desktops. This will be the set of desktops whose
        /// names match one of the desktop offerings hostname prefixes and has a numeric suffix
        /// </summary>
        /// <param name="machines">The raw set of virtual machines from the CloudStack API</param>
        /// <param name="desktopOfferings">The set of desktop offerings to use as a filter</param>
        /// <returns>A list of potential desktops (ordered by name)</returns>
        private IEnumerable<IDesktop> FilterDesktops(VirtualMachine[] machines, IEnumerable<IDesktopOffering> desktopOfferings) {
           
            SortedList<string, IDesktop> result = new SortedList<string, IDesktop>();
            foreach (VirtualMachine vm in machines) {                
                if (desktopOfferings.Count(o => {
                    int num;
                    return (vm.Name.StartsWith(o.HostnamePrefix) && int.TryParse(vm.Name.Substring(o.HostnamePrefix.Length), out num));
                }) > 0) {
                    DesktopState state = Parse(vm.State);
                    result.Add(vm.Name, new Desktop(vm.Id, vm.Name, vm.Nic[0].IpAddress, state));
                }
            }
            return result.Values;
        }

        private DesktopState Parse(string state) {
            DesktopState result = DesktopState.Unknown;
            if (!Enum.TryParse(state, true, out result)) {
                CtxTrace.TraceError("Unable to parse desktop state: {0}", state);
            }
            return result;
        }

        /// <summary>
        /// Generate a new desktop name for the specified desktop offering
        /// </summary>
        /// <param name="offering">Desktop offering</param>
        /// <returns>A name for the desktop</returns>
        private string GetNextDesktopName(IDesktopOffering offering) {
            IEnumerable<IDesktop> existingDesktops = ListDesktops().Where(d => (d.Name.StartsWith(offering.HostnamePrefix)));
            string suffix = DesktopSuffixFormat;
            int last = 0;
            if (existingDesktops.Count() > 0) {
                IEnumerable<int> existingSuffixes = existingDesktops.Select(d => {
                    int num = -1;
                    int.TryParse(d.Name.Substring(offering.HostnamePrefix.Length), out num);
                    return num;
                }).Where(i => (i != -1));
                while (existingSuffixes.Contains(last)) {
                    last++;
                }       
                suffix = last.ToString(DesktopSuffixFormat);
            }
            return string.Format("{0}{1}", offering.HostnamePrefix, suffix);
        }
        #endregion
    }
}