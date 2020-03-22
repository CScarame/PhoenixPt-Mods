﻿using Harmony;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Levels.Factions;
using PhoenixPoint.Geoscape.View;
using PhoenixPoint.Geoscape.View.ViewStates;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using static System.Reflection.BindingFlags;

namespace Sheepy.PhoenixPt.GlobeTweaks {

   public class ModConfig {
      public string Settings_Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
      public bool Center_On_New_Base = true;
      public bool Centre_On_Heal = true;
      public bool No_Auto_Unpause = true;
      public bool Pause_On_Heal = true;
   }

   public class Mod : ZyMod {
      private static PropertyInfo ContextGetter;

      public static void Init () => new Mod().MainMod();

      public void MainMod ( Func< string, object, object > api = null ) {
         SetApi( api, out ModConfig config );

         if ( config.Center_On_New_Base ) {
            Patch( typeof( GeoPhoenixFaction ), "ActivatePhoenixBase", postfix: nameof( AfterActivatePhoenixBase_Center ) );
            Patch( typeof( GeoFaction ), "OnVehicleSiteExplored", postfix: nameof( AfterExplore_Center ) );
         }

         if ( config.No_Auto_Unpause ) {
            ContextGetter = typeof( GeoscapeViewState ).GetProperty( "Context", NonPublic | Instance );
            if ( ContextGetter != null )
               Patch( typeof( UIStateVehicleSelected ), "AddTravelSite", nameof( BeforeAddTravelSite_GetPause ), nameof( AfterAddTravelSite_RestorePause ) );
         }

         if ( config.Centre_On_Heal )
            Patch( typeof( GeoscapeLog ), "ProcessQueuedEvents", nameof( BeforeProcessQueuedEvents_Centre ) );

         if ( config.Pause_On_Heal )
            Patch( typeof( GeoscapeLog ), "ProcessQueuedEvents", nameof( BeforeProcessQueuedEvents_Pause ) );
      }

      #region CenterOnNewBase
      private static void AfterActivatePhoenixBase_Center ( GeoSite site, GeoLevelController ____level ) { try {
         ____level.View.ChaseTarget( site, false );
      } catch ( Exception ex ) { Error( ex ); } }

      private static void AfterExplore_Center ( GeoFaction __instance, GeoVehicle vehicle, GeoLevelController ____level ) { try {
         if ( __instance != ____level.PhoenixFaction ) return;
         ____level.View.ChaseTarget( vehicle.CurrentSite, false );
      } catch ( Exception ex ) { Error( ex ); } }
      #endregion

      #region NoAutoUnpause
      private static GeoscapeViewContext getContext ( UIStateVehicleSelected __instance ) => (GeoscapeViewContext) ContextGetter.GetValue( __instance );

      [ HarmonyPriority( Priority.VeryHigh ) ]
      private static void BeforeAddTravelSite_GetPause ( UIStateVehicleSelected __instance, ref bool __state ) { try {
         __state = getContext( __instance ).Level.Timing.Paused;
      } catch ( Exception ex ) { Error( ex ); } }

      private static void AfterAddTravelSite_RestorePause ( UIStateVehicleSelected __instance, ref bool __state ) { try {
         getContext( __instance ).Level.Timing.Paused = __state;
      } catch ( Exception ex ) { Error( ex ); } }
      #endregion

      #region CentreOnHeal
      private static void BeforeProcessQueuedEvents_Centre ( List<IGeoCharacterContainer> ____justRestedContainer, GeoLevelController ____level ) { try {
         var container = ____justRestedContainer?.FirstOrDefault();
         if ( container is GeoVehicle vehicle && ____level.View.CurrentViewState is UIStateVehicleSelected state )
            typeof( UIStateVehicleSelected ).GetMethod( "SelectVehicle", NonPublic | Instance ).Invoke( state, new object[]{ vehicle, true } );
         else if ( container is GeoActor actor )
            ____level.View.ChaseTarget( actor, false );
      } catch ( Exception ex ) { Error( ex ); } }
      #endregion

      #region PauseOnHeal
      private static void BeforeProcessQueuedEvents_Pause ( List<IGeoCharacterContainer> ____justRestedContainer, GeoLevelController ____level ) { try {
         if ( ____justRestedContainer?.Any() == true )
            ____level.Timing.Paused = true;
      } catch ( Exception ex ) { Error( ex ); } }
      #endregion
   }
}
