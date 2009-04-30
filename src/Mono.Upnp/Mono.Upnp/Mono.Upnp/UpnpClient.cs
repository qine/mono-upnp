﻿//
// UpnpClient.cs
//
// Author:
//   Scott Peterson <lunchtimemama@gmail.com>
//
// Copyright (C) 2008 S&S Black Ltd.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;

using Mono.Ssdp;
using Mono.Upnp.Description;
using Mono.Upnp.Internal;

namespace Mono.Upnp
{
	public class UpnpClient
	{
        readonly Dictionary<DeviceAnnouncement, DeviceAnnouncement> devices =
            new Dictionary<DeviceAnnouncement, DeviceAnnouncement> ();
        readonly Dictionary<ServiceAnnouncement, ServiceAnnouncement> services =
            new Dictionary<ServiceAnnouncement, ServiceAnnouncement> ();
        readonly Dictionary<string, DeviceDescription> descriptions =
            new Dictionary<string, DeviceDescription> ();

        readonly Mono.Ssdp.Client client = new Mono.Ssdp.Client ();

        IDeserializerFactory deserializer_factory;

        public UpnpClient ()
            : this (null)
        {
        }

        public UpnpClient (IDeserializerFactory deserializerFactory)
        {
            DeserializerFactory = deserializerFactory ?? DeserializerFactory<Deserializer>.Instance;
            client.ServiceAdded += ClientServiceAdded;
            client.ServiceRemoved += ClientServiceRemoved;
        }
        
        public event EventHandler<DeviceEventArgs> DeviceAdded;
        public event EventHandler<DeviceEventArgs> DeviceRemoved;
        public event EventHandler<ServiceEventArgs> ServiceAdded;
        public event EventHandler<ServiceEventArgs> ServiceRemoved;

        public IDeserializerFactory DeserializerFactory {
            get { return deserializer_factory; }
            set {
                if (value == null) throw new ArgumentNullException ("value");
                deserializer_factory = value;
            }
        }

        public void BrowseAll ()
        {
            client.BrowseAll ();
        }

        public void Browse (TypeInfo type)
        {
            client.Browse (type.ToString ());
        }

        void ClientServiceAdded (object sender, Mono.Ssdp.ServiceArgs args)
        {
            ClientServiceEvent (args,
                (device) => {
                    if (!devices.ContainsKey (device)) {
                        OnDeviceAdded (new DeviceEventArgs (device, UpnpOperation.Added));
                        devices.Add (device, device);
                    }
                },
                (service) => {
                    if (!services.ContainsKey (service)) {
                        OnServiceAdded (new ServiceEventArgs (service, UpnpOperation.Added));
                        services.Add (service, service);
                    }
                }
            );
        }

        void ClientServiceRemoved (object sender, Mono.Ssdp.ServiceArgs args)
        {
            ClientServiceEvent (args,
                (device) => {
                    if (devices.ContainsKey (device)) {
						device.Dispose ();
                        OnDeviceRemoved (new DeviceEventArgs (device, UpnpOperation.Removed));
                        devices.Remove (device);
                    }
                },
                (service) => {
                    if (services.ContainsKey (service)) {
                        service.Dispose ();
                        OnServiceRemoved (new ServiceEventArgs (service, UpnpOperation.Removed));
                        services.Remove (service);
                    }
                }
            );
        }

        void ClientServiceEvent (Mono.Ssdp.ServiceArgs args,
                                 Action<DeviceAnnouncement> deviceHandler,
                                 Action<ServiceAnnouncement> serviceHandler)
        {
            if (!args.Usn.StartsWith ("uuid:")) {
                return;
            }

            var colon = args.Usn.IndexOf (':', 5);
            var usn = colon == -1 ? args.Usn : args.Usn.Substring (0, colon);

            if (args.Usn.Contains (":device:")) {
                var type = new DeviceType (args.Service.ServiceType);
                var device = new DeviceAnnouncement (this, type, usn, args.Service.Locations);
                deviceHandler (device);
            } else if (args.Usn.Contains (":service:")) {
                var type = new ServiceType (args.Service.ServiceType);
                var service = new ServiceAnnouncement (this, type, usn, args.Service.Locations);
                serviceHandler (service);
            }
        }
		
		protected virtual void OnDeviceAdded (DeviceEventArgs e)
		{
            OnEvent (DeviceAdded, e);
		}

        protected virtual void OnServiceAdded (ServiceEventArgs e)
        {
            OnEvent (ServiceAdded, e);
        }

        protected virtual void OnDeviceRemoved (DeviceEventArgs e)
        {
            OnEvent (DeviceRemoved, e);
        }

        protected virtual void OnServiceRemoved (ServiceEventArgs e)
        {
            OnEvent (ServiceRemoved, e);
        }
        
        void OnEvent<T> (EventHandler<T> handler, T e) where T : EventArgs
        {
            if (handler != null) {
                handler (this, e);
            }
        }

        internal ServiceDescription GetDescription (ServiceAnnouncement announcement)
        {
            foreach (var uri in announcement.Locations) {
                if (descriptions.ContainsKey (uri)) {
                    var description = GetDescription (announcement, descriptions[uri]);
                    if (description != null && !description.IsDisposed) {
                        return description;
                    }
                }
                try {
                    var deserializer = deserializer_factory.CreateDeserializer ();
                    var rootDevice = deserializer.DeserializeDescription (new Uri (uri));
                    if (rootDevice == null) {
                        continue;
                    }
                    descriptions[uri] = rootDevice;
                    return GetDescription (announcement, rootDevice);
                } catch (Exception e) {
                    Log.Exception (string.Format ("There was a problem fetching the description at {0}.", uri), e);
                }
            }
            return null;
        }

        internal DeviceDescription GetDescription (DeviceAnnouncement announcement)
        {
            foreach (var uri in announcement.Locations) {
                if (descriptions.ContainsKey (uri)) {
                    var description = GetDescription (announcement, descriptions[uri]);
                    if (description != null && !description.IsDisposed) {
                        return description;
                    }
                }
                try {
                    var deserializer = deserializer_factory.CreateDeserializer ();
                    var rootDevice = deserializer.DeserializeDescription (new Uri (uri));
                    if (rootDevice == null) {
                        continue;
                    }
                    descriptions[uri] = rootDevice;
                    return GetDescription (announcement, rootDevice);
                } catch (Exception e) {
                    Log.Exception (string.Format ("There was a problem fetching the description at {0}.", uri), e);
                }
            }
            return null;
        }

        ServiceDescription GetDescription (ServiceAnnouncement announcement, DeviceDescription device)
        {
            foreach (var description in device.Services) {
                if (device.Udn == announcement.DeviceUdn && announcement.Type == description.Type) {
                    return description;
                }
            }
            foreach (var childDevice in device.Devices) {
                var description = GetDescription (announcement, childDevice);
                if (description != null) {
                    return description;
                }
            }
            return null;
        }

        DeviceDescription GetDescription (DeviceAnnouncement announcement, DeviceDescription device)
        {
            if (device.Type == announcement.Type && device.Udn == announcement.Udn) {
                return device;
            }
            foreach (var childDevice in device.Devices) {
                var description = GetDescription (announcement, childDevice);
                if (description != null) {
                    return description;
                }
            }
            return null;
        }
	}
}