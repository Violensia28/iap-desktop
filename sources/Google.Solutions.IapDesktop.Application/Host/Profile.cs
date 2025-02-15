﻿//
// Copyright 2022 Google LLC
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

using Google.Apis.Util;
using Google.Solutions.Common.Util;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Google.Solutions.IapDesktop.Application.Host
{
    public sealed class Profile : IDisposable
    {
        public const string DefaultProfileName = "Default";
        private const string DefaultProfileKey = "1.0";
        private const string ProfileKeyPrefix = DefaultProfileKey + ".";

        private static readonly Regex ProfileNamePattern = new Regex(@"^[a-zA-Z0-9_\-\ ]+$");

        /// <summary>
        /// Path containing profiles.
        /// </summary>
        private const string ProfilesKeyPath = @"Software\Google\IapDesktop";

        /// <summary>
        /// Path to policies. This path is independent of the profile.
        /// </summary>
        private const string PoliciesKeyPath = @"Software\Policies\Google\IapDesktop\1.0";

        /// <summary>
        /// Settings key. This is never null.
        /// </summary>
        public RegistryKey SettingsKey { get; private set; }

        /// <summary>
        /// Key for machine policies, can be null.
        /// </summary>
        public RegistryKey MachinePolicyKey { get; private set; }

        /// <summary>
        /// Key for user policies, can be null.
        /// </summary>
        public RegistryKey UserPolicyKey { get; private set; }

        /// <summary>
        /// Name of the profile.
        /// </summary>
        public string Name { get; private set; }

        public bool IsDefault { get; private set; }

        private Profile()
        {
        }

        public static bool IsValidProfileName(string name)
        {
            return name != null &&
                !name.Equals(DefaultProfileName, StringComparison.OrdinalIgnoreCase) &&
                name.Trim() == name &&
                name.Length < 32 &&
                ProfileNamePattern.IsMatch(name);
        }

        public static Profile CreateProfile(string name)
        {
            if (!IsValidProfileName(name))
            {
                throw new ArgumentException("Invalid profile name");
            }

            using (var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            {
                hkcu.CreateSubKey($@"{ProfilesKeyPath}\{ProfileKeyPrefix}{name}");
            }

            return OpenProfile(name);
        }

        public static Profile OpenProfile(string name)
        {
            if (name != null && !IsValidProfileName(name))
            {
                throw new ArgumentException($"Invalid profile name: {name}");
            }

            using (var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
            {
                if (name == null)
                {
                    //
                    // Open or create default profile. For backwards compatbility
                    // reasons, the default profile uses the key "1.0".
                    //
                    return new Profile()
                    {
                        Name = DefaultProfileName,
                        IsDefault = true,
                        MachinePolicyKey = hklm.OpenSubKey(PoliciesKeyPath),
                        UserPolicyKey = hkcu.OpenSubKey(PoliciesKeyPath),
                        SettingsKey = hkcu.CreateSubKey($@"{ProfilesKeyPath}\{DefaultProfileKey}")
                    };
                }
                else
                {
                    //
                    // Open existing profile.
                    //
                    var settingsKey = hkcu.OpenSubKey($@"{ProfilesKeyPath}\{ProfileKeyPrefix}{name}", true);
                    if (settingsKey == null)
                    {
                        throw new ProfileNotFoundException("Unknown profile: " + name);
                    }

                    return new Profile()
                    {
                        Name = name,
                        IsDefault = false,
                        MachinePolicyKey = hklm.OpenSubKey(PoliciesKeyPath),
                        UserPolicyKey = hkcu.OpenSubKey(PoliciesKeyPath),
                        SettingsKey = settingsKey
                    };
                }
            }
        }

        public static void DeleteProfile(string name)
        {
            using (var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            {
                var path = $@"{Profile.ProfilesKeyPath}\{ProfileKeyPrefix}{name}";
                using (var key = hkcu.OpenSubKey(path))
                {
                    if (key != null)
                    {
                        hkcu.DeleteSubKeyTree(path);
                    }
                }
            }
        }

        public static IEnumerable<string> ListProfiles()
        {
            using (var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            using (var profiles = hkcu.OpenSubKey(ProfilesKeyPath))
            {
                if (profiles == null)
                {
                    return Enumerable.Empty<string>();
                }
                else
                {
                    return profiles.GetSubKeyNames()
                        .EnsureNotNull()
                        .Select(n => n == DefaultProfileKey
                            ? DefaultProfileName
                            : n.Substring(ProfileKeyPrefix.Length));
                }
            }
        }

        public void Dispose()
        {
            this.SettingsKey.Dispose();
            this.UserPolicyKey?.Dispose();
            this.MachinePolicyKey?.Dispose();
        }
    }

    public class ProfileNotFoundException : Exception
    {
        public ProfileNotFoundException(string message)
            : base(message)
        {
        }
    }
}
