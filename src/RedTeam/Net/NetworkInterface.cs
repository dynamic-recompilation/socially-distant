﻿using RedTeam.SaveData;

namespace RedTeam.Net
{
    public class NetworkInterface
    {
        private NetworkSimulation _simulation;
        private DeviceNode _devNode;
        
        
        public NetworkInterface(NetworkSimulation simulation, DeviceNode devNode)
        {
            _simulation = simulation;
            _devNode = devNode;
        }

        public uint LocalAddress
            => (NetworkAddress & SubnetMask) | (_devNode.Address & ~SubnetMask);

        public uint SubnetMask
            => _devNode.Network.SubnetMask;

        public uint PublicAddress
            => _devNode.Network.PublicAddress;

        public uint NetworkAddress
            => _devNode.Network.SubnetAddress;

        public WebNode MapAddressToNode(uint address)
        {
            // Loop-back address (127.0.0.1) maps to ourself.
            if (address == NetworkHelpers.LoopbackAddress)
                return this._devNode;

            // Is the specified address inside of our network?
            if ((address & this.SubnetMask) == this.NetworkAddress)
            {
                // Map to a device node on our network.
                return _devNode.FindOtherLocalDevice(address);
            }
            
            // Address isn't in our LAN, attempt a tier 3 lookup.
            else
            {
                return _devNode.NetworkLookup(address);
            }
            
            

            return null;
        }

        public int GetHopsCount(WebNode destNode)
        {
            return _simulation.CalculateHops(this._devNode, destNode);
        }
    }
}