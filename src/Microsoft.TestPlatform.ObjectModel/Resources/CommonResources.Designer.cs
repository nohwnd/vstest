﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Resources {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [System.Obsolete]
    public class CommonResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal CommonResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.VisualStudio.TestPlatform.ObjectModel.Resources.CommonResources", typeof(CommonResources).GetTypeInfo().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to The parameter cannot be null or empty..
        /// </summary>
        public static string CannotBeNullOrEmpty {
            get {
                return ResourceManager.GetString("CannotBeNullOrEmpty", resourceCulture);
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to Test run detected DLL(s) which were built for different framework and platform versions. Following DLL(s) do not match current settings, which are {0} framework and {1} platform.{2}Go to {3} for more details on managing these settings..
        /// </summary>
        public static string DisplayChosenSettings {
            get {
                return ResourceManager.GetString("DisplayChosenSettings", resourceCulture);
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to Settings file provided does not conform to required format..
        /// </summary>
        public static string MalformedRunSettingsFile {
            get {
                return ResourceManager.GetString("MalformedRunSettingsFile", resourceCulture);
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to None of the provided test containers match the Platform Architecture and .Net Framework settings for the test run. Platform: {0}  .Net Framework: {1}. Go to http://go.microsoft.com/fwlink/?LinkID=330428 for more details on managing these settings..
        /// </summary>
        public static string NoMatchingSourcesFound {
            get {
                return ResourceManager.GetString("NoMatchingSourcesFound", resourceCulture);
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to {0} is built for Framework {1} and Platform {2}..
        /// </summary>
        public static string SourceIncompatible {
            get {
                return ResourceManager.GetString("SourceIncompatible", resourceCulture);
            }
        }
    }
}
