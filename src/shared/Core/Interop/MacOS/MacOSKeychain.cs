using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GitCredentialManager.Interop.MacOS.Native;
using static GitCredentialManager.Interop.MacOS.Native.CoreFoundation;
using static GitCredentialManager.Interop.MacOS.Native.SecurityFramework;

namespace GitCredentialManager.Interop.MacOS
{
    public class MacOSKeychain : ICredentialStore
    {
        private readonly string _namespace;
        private readonly string _accessGroup;

        #region Constructors

        /// <summary>
        /// Open the default keychain (current user's login keychain).
        /// </summary>
        /// <param name="namespace">Optional namespace to scope credential operations.</param>
        /// <param name="accessGroup">
        /// Optional Keychain access group used when creating and searching for items.
        /// If not specified, the application's default access group is used.
        /// </param>
        /// <returns>Default keychain.</returns>
        public MacOSKeychain(string @namespace = null, string accessGroup = null)
        {
            PlatformUtils.EnsureMacOS();
            _namespace = @namespace;
            _accessGroup = accessGroup;
        }

        #endregion

        #region ICredentialStore

        public ICredential Get(string service, string account)
        {
            IntPtr query = IntPtr.Zero;
            IntPtr resultPtr = IntPtr.Zero;
            IntPtr servicePtr = IntPtr.Zero;
            IntPtr accountPtr = IntPtr.Zero;
            IntPtr groupPtr = IntPtr.Zero;

            try
            {
                query = CFDictionaryCreateMutable(
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero, IntPtr.Zero);

                CFDictionaryAddValue(query, kSecClass, kSecClassGenericPassword);
                CFDictionaryAddValue(query, kSecMatchLimit, kSecMatchLimitOne);
                CFDictionaryAddValue(query, kSecReturnData, kCFBooleanTrue);
                CFDictionaryAddValue(query, kSecReturnAttributes, kCFBooleanTrue);

                if (!string.IsNullOrWhiteSpace(service))
                {
                    string fullService = CreateServiceName(service);
                    servicePtr = CreateCFStringUtf8(fullService);
                    CFDictionaryAddValue(query, kSecAttrService, servicePtr);
                }

                if (!string.IsNullOrWhiteSpace(account))
                {
                    accountPtr = CreateCFStringUtf8(account);
                    CFDictionaryAddValue(query, kSecAttrAccount, accountPtr);
                }

                if (!string.IsNullOrWhiteSpace(_accessGroup))
                {
                    groupPtr = CreateCFStringUtf8(_accessGroup);
                    CFDictionaryAddValue(query, kSecAttrAccessGroup, groupPtr);
                }

                int searchResult = SecItemCopyMatching(query, out resultPtr);

                switch (searchResult)
                {
                    case OK:
                        int typeId = CFGetTypeID(resultPtr);
                        Debug.Assert(typeId != CFArrayGetTypeID(), "Returned more than one keychain item in search");
                        if (typeId == CFDictionaryGetTypeID())
                        {
                            return CreateCredentialFromAttributes(resultPtr);
                        }

                        throw new InteropException($"Unknown keychain search result type CFTypeID: {typeId}.", -1);

                    case ErrorSecItemNotFound:
                        return null;

                    default:
                        ThrowIfError(searchResult);
                        return null;
                }
            }
            finally
            {
                if (query != IntPtr.Zero) CFRelease(query);
                if (servicePtr != IntPtr.Zero) CFRelease(servicePtr);
                if (accountPtr != IntPtr.Zero) CFRelease(accountPtr);
                if (groupPtr != IntPtr.Zero) CFRelease(groupPtr);
                if (resultPtr != IntPtr.Zero) CFRelease(resultPtr);
            }
        }

        public void AddOrUpdate(string service, string account, string secret)
        {
            EnsureArgument.NotNullOrWhiteSpace(service, nameof(service));

            IntPtr query = IntPtr.Zero;
            IntPtr servicePtr = IntPtr.Zero;
            IntPtr accountPtr = IntPtr.Zero;
            IntPtr groupPtr = IntPtr.Zero;
            IntPtr resultPtr = IntPtr.Zero;

            try
            {
                // Check if an entry already exists in the keychain
                query = CFDictionaryCreateMutable(
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero, IntPtr.Zero);

                CFDictionaryAddValue(query, kSecClass, kSecClassGenericPassword);
                CFDictionaryAddValue(query, kSecMatchLimit, kSecMatchLimitOne);
                CFDictionaryAddValue(query, kSecReturnRef, kCFBooleanTrue);

                if (!string.IsNullOrWhiteSpace(service))
                {
                    string fullService = CreateServiceName(service);
                    servicePtr = CreateCFStringUtf8(fullService);
                    CFDictionaryAddValue(query, kSecAttrService, servicePtr);
                }

                if (!string.IsNullOrWhiteSpace(account))
                {
                    accountPtr = CreateCFStringUtf8(account);
                    CFDictionaryAddValue(query, kSecAttrAccount, accountPtr);
                }

                if (!string.IsNullOrWhiteSpace(_accessGroup))
                {
                    groupPtr = CreateCFStringUtf8(_accessGroup);
                    CFDictionaryAddValue(query, kSecAttrAccessGroup, groupPtr);
                }

                int searchResult = SecItemCopyMatching(query, out resultPtr);

                switch (searchResult)
                {
                    // Update existing entry
                    case OK:
                        Update(query, service, account, secret);
                        break;

                    // Create new entry
                    case ErrorSecItemNotFound:
                        Add(service, account, secret);
                        break;

                    default:
                        ThrowIfError(searchResult);
                        break;
                }
            }
            finally
            {
                if (query != IntPtr.Zero) CFRelease(query);
                if (servicePtr != IntPtr.Zero) CFRelease(servicePtr);
                if (accountPtr != IntPtr.Zero) CFRelease(accountPtr);
                if (groupPtr != IntPtr.Zero) CFRelease(groupPtr);
                if (resultPtr != IntPtr.Zero) CFRelease(resultPtr);
            }
        }

        private void Add(string service, string account, string secret)
        {
            IntPtr dict = IntPtr.Zero;
            IntPtr servicePtr = IntPtr.Zero;
            IntPtr accountPtr = IntPtr.Zero;
            IntPtr dataPtr = IntPtr.Zero;
            IntPtr groupPtr = IntPtr.Zero;
            IntPtr resultPtr = IntPtr.Zero;

            byte[] data = Encoding.UTF8.GetBytes(secret);

            try
            {
                dict = CFDictionaryCreateMutable(
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero, IntPtr.Zero);

                CFDictionaryAddValue(dict, kSecClass, kSecClassGenericPassword);

                dataPtr = CFDataCreate(IntPtr.Zero, data, data.Length);
                CFDictionaryAddValue(dict, kSecValueData, dataPtr);

                string fullService = CreateServiceName(service);
                servicePtr = CreateCFStringUtf8(fullService);
                CFDictionaryAddValue(dict, kSecAttrService, servicePtr);

                if (!string.IsNullOrWhiteSpace(account))
                {
                    accountPtr = CreateCFStringUtf8(account);
                    CFDictionaryAddValue(dict, kSecAttrAccount, accountPtr);
                }

                if (!string.IsNullOrWhiteSpace(_accessGroup))
                {
                    groupPtr = CreateCFStringUtf8(_accessGroup);
                    CFDictionaryAddValue(dict, kSecAttrAccessGroup, groupPtr);
                }

                ThrowIfError(
                    SecItemAdd(dict, out resultPtr),
                    "Failed to add new entry to keychain."
                );
            }
            finally
            {
                if (dict != IntPtr.Zero) CFRelease(dict);
                if (servicePtr != IntPtr.Zero) CFRelease(servicePtr);
                if (accountPtr != IntPtr.Zero) CFRelease(accountPtr);
                if (dataPtr != IntPtr.Zero) CFRelease(dataPtr);
                if (groupPtr != IntPtr.Zero) CFRelease(groupPtr);
                if (resultPtr != IntPtr.Zero) CFRelease(resultPtr);
            }
        }

        private void Update(IntPtr query, string service, string account, string secret)
        {
            IntPtr dict = IntPtr.Zero;
            IntPtr servicePtr = IntPtr.Zero;
            IntPtr accountPtr = IntPtr.Zero;
            IntPtr dataPtr = IntPtr.Zero;
            IntPtr groupPtr = IntPtr.Zero;
            IntPtr resultPtr = IntPtr.Zero;

            byte[] data = Encoding.UTF8.GetBytes(secret);

            try
            {
                dict = CFDictionaryCreateMutable(
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero, IntPtr.Zero);

                dataPtr = CFDataCreate(IntPtr.Zero, data, data.Length);
                CFDictionaryAddValue(dict, kSecValueData, dataPtr);

                string fullService = CreateServiceName(service);
                servicePtr = CreateCFStringUtf8(fullService);
                CFDictionaryAddValue(dict, kSecAttrService, servicePtr);

                if (!string.IsNullOrWhiteSpace(account))
                {
                    accountPtr = CreateCFStringUtf8(account);
                    CFDictionaryAddValue(dict, kSecAttrAccount, accountPtr);
                }

                if (!string.IsNullOrWhiteSpace(_accessGroup))
                {
                    groupPtr = CreateCFStringUtf8(_accessGroup);
                    CFDictionaryAddValue(query, kSecAttrAccessGroup, groupPtr);
                }

                ThrowIfError(
                    SecItemUpdate(query, dict),
                    "Failed to update existing keychain entry."
                );
            }
            finally
            {
                if (dict != IntPtr.Zero) CFRelease(dict);
                if (servicePtr != IntPtr.Zero) CFRelease(servicePtr);
                if (accountPtr != IntPtr.Zero) CFRelease(accountPtr);
                if (dataPtr != IntPtr.Zero) CFRelease(dataPtr);
                if (groupPtr != IntPtr.Zero) CFRelease(groupPtr);
                if (resultPtr != IntPtr.Zero) CFRelease(resultPtr);
            }
        }

        public bool Remove(string service, string account)
        {
            IntPtr query = IntPtr.Zero;
            IntPtr servicePtr = IntPtr.Zero;
            IntPtr accountPtr = IntPtr.Zero;
            IntPtr groupPtr = IntPtr.Zero;

            try
            {
                query = CFDictionaryCreateMutable(
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero, IntPtr.Zero);

                CFDictionaryAddValue(query, kSecClass, kSecClassGenericPassword);
                CFDictionaryAddValue(query, kSecMatchLimit, kSecMatchLimitOne);

                if (!string.IsNullOrWhiteSpace(service))
                {
                    string fullService = CreateServiceName(service);
                    servicePtr = CreateCFStringUtf8(fullService);
                    CFDictionaryAddValue(query, kSecAttrService, servicePtr);
                }

                if (!string.IsNullOrWhiteSpace(account))
                {
                    accountPtr = CreateCFStringUtf8(account);
                    CFDictionaryAddValue(query, kSecAttrAccount, accountPtr);
                }

                if (!string.IsNullOrWhiteSpace(_accessGroup))
                {
                    groupPtr = CreateCFStringUtf8(_accessGroup);
                    CFDictionaryAddValue(query, kSecAttrAccessGroup, groupPtr);
                }

                // Delete credentials matched by the query
                int deleteResult = SecItemDelete(query);
                switch (deleteResult)
                {
                    case OK:
                        // Item was deleted
                        return true;

                    case ErrorSecItemNotFound:
                        return false;

                    default:
                        ThrowIfError(deleteResult);
                        return false;
                }
            }
            finally
            {
                if (query != IntPtr.Zero) CFRelease(query);
                if (servicePtr != IntPtr.Zero) CFRelease(servicePtr);
                if (accountPtr != IntPtr.Zero) CFRelease(accountPtr);
                if (groupPtr != IntPtr.Zero) CFRelease(groupPtr);
            }
        }

        #endregion

        private static IntPtr CreateCFStringUtf8(string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            return CFStringCreateWithBytes(IntPtr.Zero,
                bytes, bytes.Length, CFStringEncoding.kCFStringEncodingUTF8, false);
        }

        private static ICredential CreateCredentialFromAttributes(IntPtr attributes)
        {
            string service = GetStringAttribute(attributes, kSecAttrService);
            string account = GetStringAttribute(attributes, kSecAttrAccount);
            string password = GetStringAttribute(attributes, kSecValueData);
            string label = GetStringAttribute(attributes, kSecAttrLabel);
            return new MacOSKeychainCredential(service, account, password, label);
        }

        private static string GetStringAttribute(IntPtr dict, IntPtr key)
        {
            if (dict == IntPtr.Zero)
            {
                return null;
            }

            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (CFDictionaryGetValueIfPresent(dict, key, out IntPtr value) && value != IntPtr.Zero)
                {
                    if (CFGetTypeID(value) == CFStringGetTypeID())
                    {
                        int stringLength = (int)CFStringGetLength(value);
                        int bufferSize = stringLength + 1;
                        buffer = Marshal.AllocHGlobal(bufferSize);
                        if (CFStringGetCString(value, buffer, bufferSize, CFStringEncoding.kCFStringEncodingUTF8))
                        {
                            return Marshal.PtrToStringAuto(buffer, stringLength);
                        }
                    }

                    if (CFGetTypeID(value) == CFDataGetTypeID())
                    {
                        int length = CFDataGetLength(value);
                        IntPtr ptr = CFDataGetBytePtr(value);
                        return Marshal.PtrToStringAuto(ptr, length);
                    }
                }
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return null;
        }

        private string CreateServiceName(string service)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(_namespace))
            {
                sb.AppendFormat("{0}:", _namespace);
            }

            sb.Append(service);
            return sb.ToString();
        }
    }
}
