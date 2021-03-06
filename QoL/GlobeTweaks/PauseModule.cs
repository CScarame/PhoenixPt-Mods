﻿using Base.Core;
using Base.UI;
using Harmony;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Entities.Abilities;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Levels.Factions;
using PhoenixPoint.Geoscape.View;
using PhoenixPoint.Geoscape.View.ViewStates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static System.Reflection.BindingFlags;

namespace Sheepy.PhoenixPt.GlobeTweaks {
   internal class PauseModule : ZyMod {

      private static PropertyInfo ContextGetter;

      public void DoPatches () {
         if ( ! ( Mod.Config.Auto_Unpause_Multiple && Mod.Config.Auto_Unpause_Single ) ) {
            ContextGetter = typeof( GeoscapeViewState ).GetProperty( "Context", NonPublic | Instance );
            if ( ContextGetter != null ) {
               // All moves (<1.5.2) / Right-click move (>=1.5.2)
               TryPatch( typeof( UIStateVehicleSelected ), "AddTravelSite", nameof( BeforeMove_GetPause ), nameof( AfterAddTravelSite_RestorePause ) );
               // Menu move (>=1.5.2)
               TryPatch( typeof( UIStateVehicleSelected ), "OnContextualItemSelected", nameof( BeforeMove_GetPause ), nameof( AfterContextMove_RestorePause ) );
            } else
               Warn( "GeoscapeViewState.Context not found." );
         }

         if ( Mod.Config.Base_Centre_On_Heal )
            TryPatch( typeof( GeoscapeLog ), "ProcessQueuedEvents", nameof( BeforeQueuedEvents_CentreBase ) );
         if ( Mod.Config.Base_Pause_On_Heal )
            TryPatch( typeof( GeoscapeLog ), "ProcessQueuedEvents", nameof( BeforeQueuedEvents_PauseBase ) );

         if ( Mod.Config.Vehicle_Centre_On_Heal )
            TryPatch( typeof( GeoscapeLog ), "ProcessQueuedEvents", nameof( BeforeQueuedEvents_CentreVehicle ) );
         if ( Mod.Config.Vehicle_Pause_On_Heal )
            TryPatch( typeof( GeoscapeLog ), "ProcessQueuedEvents", nameof( BeforeQueuedEvents_PauseVehicle ) );

         if ( Mod.Config.Notice_On_HP_Only_Heal || Mod.Config.Notice_On_Stamina_Only_Heal ) {
            BatchPatch( () => {
               Patch( typeof( GeoscapeLog ), "Health_StatChangeEvent"   , nameof( BeforeHealthChange_SetHP ), nameof( AfterHealthChange_UnsetHP ) );
               Patch( typeof( GeoscapeLog ), "Stamina_StatChangeEvent"  , nameof( BeforeHealthChange_SetST ), nameof( AfterHealthChange_UnsetST ) );
               Patch( typeof( GeoscapeLog ), "CheckForRestedInContainer", postfix: nameof( CheckHPSTRested ) );
               TryPatch( typeof( GeoscapeLog ), "AddEntry", postfix: nameof( BeforeAddEntry_AmendEntry ) );
            } );
         }
      }

      #region NoAutoUnpause
      private static GeoscapeViewContext getContext ( UIStateVehicleSelected __instance ) => (GeoscapeViewContext) ContextGetter.GetValue( __instance );

      [ HarmonyPriority( Priority.VeryHigh ) ]
      private static void BeforeMove_GetPause ( UIStateVehicleSelected __instance, ref bool __state ) { try {
         __state = getContext( __instance ).Level.Timing.Paused;
         //Verbo( "New travel site. Paused = {0}", __state );
      } catch ( Exception ex ) { Error( ex ); } }

      private static void AfterContextMove_RestorePause ( UIStateVehicleSelected __instance, GeoAbility ability, ref bool __state ) { try {
         if ( ! ( ability is MoveVehicleAbility ) ) return;
         AfterAddTravelSite_RestorePause( __instance, ref __state );
      } catch ( Exception ex ) { Error( ex ); } }

      private static void AfterAddTravelSite_RestorePause ( UIStateVehicleSelected __instance, ref bool __state ) { try {
         var craft_count = GameUtl.CurrentLevel().GetComponent<GeoLevelController>().ViewerFaction.Vehicles.Count();
         //Verbo( "Craft count = {0}", craft_count );
         if ( ( craft_count <= 1 && Mod.Config.Auto_Unpause_Single ) || ( craft_count > 1 && Mod.Config.Auto_Unpause_Multiple ) ) return;
         Verbo( "New vehicle travel plan. Setting time to {0}.", __state ? "Paused" : "Running" );
         getContext( __instance ).Level.Timing.Paused = __state;
      } catch ( Exception ex ) { Error( ex ); } }
      #endregion

      #region HP/ST only pause
      private static bool HP_Changed, ST_Changed;

      [ HarmonyPriority( Priority.VeryHigh ) ]
      private static void BeforeHealthChange_SetHP  () => HP_Changed = true;
      private static void AfterHealthChange_UnsetHP () => HP_Changed = false;
      [ HarmonyPriority( Priority.VeryHigh ) ]
      private static void BeforeHealthChange_SetST  () => ST_Changed = true;
      private static void AfterHealthChange_UnsetST () => ST_Changed = false;

      private static void CheckHPSTRested ( GeoCharacter restedCharacter, List<IGeoCharacterContainer> ____justRestedContainer, GeoFaction ____faction ) { try {
         if ( ! HP_Changed && ! ST_Changed ) return;
         if ( HP_Changed && ! Mod.Config.Notice_On_HP_Only_Heal ) return;
         if ( ST_Changed && ! Mod.Config.Notice_On_Stamina_Only_Heal ) return;
         Func<IGeoCharacterContainer,bool> foundRested = ( e ) => e.GetAllCharacters().Contains( restedCharacter );

         if ( ____justRestedContainer.Any( foundRested ) ) return;
         var container = ____faction.Vehicles.FirstOrDefault( foundRested ) ?? ____faction.Sites.FirstOrDefault( foundRested );
         if ( container == null ) return;
         if ( container.GetAllCharacters().All( c => ( HP_Changed && !c.IsInjured ) || ( ST_Changed && c.Fatigue?.IsFullyRested != false ) ) ) {
            Info( "Detected {0} recovery on {1}", HP_Changed ? "HP" : "stamina", container.Name );
            ____justRestedContainer.Add( container );
         }
      } catch ( Exception ex ) { Error( ex ); } }

      private static void BeforeAddEntry_AmendEntry ( GeoscapeLogEntry entry, GeoscapeLogMessagesDef ____messagesDef, GeoFaction ____faction ) { try {
         if ( entry == null || entry.Text != ____messagesDef.AircraftCrewReadyMessage ) return;
         var name = ( entry.Parameters[0] as LocalizedTextBind )?.Localize();
         Verbo( "Checking rest message of {0}", name );

         Func<IGeoCharacterContainer,bool> foundEntry = ( e ) => e.Name == name;
         IGeoCharacterContainer container = ____faction.Vehicles.FirstOrDefault( foundEntry ) ?? ____faction.Sites.FirstOrDefault( foundEntry );
         if ( container == null ) return;

         string note;
         if ( container.GetAllCharacters().Any( e => e.IsInjured ) ) note = new LocalizedTextBind( "KEY_GEOSCAPE_STAMINA" ).Localize();
         else if ( container.GetAllCharacters().Any( e => e.Fatigue?.IsFullyRested == false ) ) note = new LocalizedTextBind( "KEY_HEALTH" ).Localize();
         else return;
         Info( "Updating rest message of {0}", name );
         entry.Text = new LocalizedTextBind( entry.GenerateMessage() + " (" + TitleCase( note ) + ")", true );
      } catch ( Exception ex ) { Error( ex ); } }
      #endregion

      #region Vehicle Heal
      private static void BeforeQueuedEvents_CentreVehicle ( List<IGeoCharacterContainer> ____justRestedContainer, GeoLevelController ____level ) { try {
         var vehicle = ____justRestedContainer?.OfType<GeoVehicle>().FirstOrDefault();
         if ( vehicle == null ) return;
         GeoscapeViewState state = ____level.View.CurrentViewState;
         Info( "Crew on {0} are rested. ViewState is {1}.", vehicle.Name, state );
         if ( state is UIStateVehicleSelected )
            typeof( UIStateVehicleSelected ).GetMethod( "SelectVehicle", NonPublic | Instance ).Invoke( state, new object[]{ vehicle, true } );
      } catch ( Exception ex ) { Error( ex ); } }

      private static void BeforeQueuedEvents_PauseVehicle ( List<IGeoCharacterContainer> ____justRestedContainer, GeoLevelController ____level ) { try {
         if ( ____justRestedContainer?.OfType<GeoVehicle>().Any() != true ) return;
         Info( "Crew on a vehicle are rested. Pausing" );
         ____level.View.RequestGamePause();
      } catch ( Exception ex ) { Error( ex ); } }
      #endregion

      #region Base Heal
      private static void BeforeQueuedEvents_CentreBase ( List<IGeoCharacterContainer> ____justRestedContainer, GeoLevelController ____level ) { try {
         var pxbase = ____justRestedContainer?.OfType<GeoSite>().FirstOrDefault();
         if ( pxbase == null ) return;
         Info( "Crew in base {0} are rested. Centering.", pxbase.Name );
         ____level.View.ChaseTarget( pxbase, false );
      } catch ( Exception ex ) { Error( ex ); } }

      private static void BeforeQueuedEvents_PauseBase ( List<IGeoCharacterContainer> ____justRestedContainer, GeoLevelController ____level ) { try {
         if ( ____justRestedContainer?.OfType<GeoSite>().Any() != true ) return;
         Info( "Crew in a base are rested. Pausing." );
         ____level.View.RequestGamePause();
      } catch ( Exception ex ) { Error( ex ); } }
      #endregion
   }
}
