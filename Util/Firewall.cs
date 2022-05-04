﻿using NetFwTypeLib;

namespace Lost_Ark_Packet_Capture
{
    public class FirewallManager
    {
        static string RuleName = "Lost Ark Packet Capture v0.3";

        public static bool RuleExists(string name)
        {
            var firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
            foreach (INetFwRule firewallRule in firewallPolicy.Rules)
                if (firewallRule.Name != null && firewallRule.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        public static bool RuleIsEnabled(string name)
        {
            var firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
            foreach (INetFwRule firewallRule in firewallPolicy.Rules)
                if (firewallRule.Name != null && firewallRule.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return firewallRule.Enabled;

            return false;
        }
        public static void EnableRule(string name)
        {
            var firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
            foreach (INetFwRule firewallRule in firewallPolicy.Rules)
                if (firewallRule.Name != null && firewallRule.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    firewallRule.Enabled = true;
                    break;
                }
        }

        public static void AllowFirewall()
        {
            if (RuleExists(RuleName))
            {
                if (!RuleIsEnabled(RuleName))
                {
                    EnableRule(RuleName);
                }
            }
            else AddRule();
        }
        public static void AddRule()
        {
            var firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
            var firewallRule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
            firewallRule.Name = RuleName;
            firewallRule.Description = "Allows Lost Ark Packet Capture to capture Lost Ark packets";
            firewallRule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
            firewallRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
            firewallRule.InterfaceTypes = "All";
            firewallRule.Protocol = 6; // TCP
            //firewallRule.RemotePorts = "6040";
            //firewallRule.ApplicationName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            firewallRule.Enabled = true;
            firewallPolicy.Rules.Add(firewallRule);
        }
    }
}