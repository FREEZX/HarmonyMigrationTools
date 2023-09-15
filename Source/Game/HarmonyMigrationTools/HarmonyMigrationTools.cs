using System;
using System.Collections.Generic;
using FlaxEngine;

namespace HarmonyMigrationTools
{
    
    /// <summary>
    /// HarmonyMigrationTools Script.
    /// </summary>
    public class HarmonyMigrationTools : GamePlugin
    { 
        /// <inheritdoc/>
        public HarmonyMigrationTools()
        {
        
        _description = new PluginDescription
        {
            Name = "Harmony Migration Tools",
            Category = "Other", //todo: fit into categories
            Description = "",
            Author = "",
            IsAlpha=true,
            IsBeta=false,
            Version=new Version(0,0,1),
            RepositoryUrl = "https://github.com/anchorlightforge/HarmonyMigrationTools"

        };
        }

        public override void Initialize()
        {
            base.Initialize();
            // Here you can add code that needs to be called when script is created, just before the first game update
        }
        
        /// <inheritdoc/>
        public override void Deinitialize()
        {
            base.Deinitialize();
            // Here you can add code that needs to be called when script is enabled (eg. register for events)
        }

    }
}
