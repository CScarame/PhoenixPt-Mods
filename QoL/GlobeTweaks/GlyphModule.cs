﻿using Base.UI;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View;
using PhoenixPoint.Geoscape.View.ViewModules;
using System;
using System.Linq;
using UnityEngine;

namespace Sheepy.PhoenixPt.GlobeTweaks {
   internal class GlyphModule : ZyMod {

      private static IPatch FovPatch;

      public void DoPatches () {
         var conf = Mod.Config.Haven_Icons;
         if ( conf.Always_Show_Action )
            FovPatch = TryPatch( typeof( GeoSite ), "Initialize", postfix: nameof( AfterInit_DisableFov ) );
         if ( conf.Popup_Show_Recruit_Class )
            TryPatch( typeof( UIModuleSelectionInfoBox ), "SetHaven", postfix: nameof( AfterSetHaven_ShowRecruitClass ) );
         if ( conf.Popup_Show_Trade )
            TryPatch( typeof( UIModuleSelectionInfoBox ), "SetHaven", postfix: nameof( AfterSetHaven_ShowResourceStock ) );
         if ( conf.Hide_Recruit_Stickman ) {
            TryPatch( typeof( GeoSiteVisualsController ), "Awake", postfix: nameof( AfterGeoVisualAwake_MoveRecruitIcon ) );
            TryPatch( typeof( GeoSiteVisualsController ), "RefreshHavenDetailsInformation", postfix: nameof( AfterHavenInfo_HideStickman ) );
         }
      }

      private static void AfterInit_DisableFov ( GeoSiteVisualsController ____visuals ) { try {
         ____visuals.FovController.ControllingDef.InvisibleOverFov = -1;
         Unpatch( ref FovPatch );
      } catch ( Exception ex ) { Error( ex ); } }

      private static string OriginalRecruitText;

      private static void AfterSetHaven_ShowRecruitClass ( UIModuleSelectionInfoBox __instance, GeoSite ____site, bool showRecruits ) { try {
         if ( ! showRecruits ) return;
         var recruit = ____site.GetComponent<GeoHaven>()?.AvailableRecruit;
         if ( recruit == null ) return;
         if ( OriginalRecruitText == null ) OriginalRecruitText = __instance.RecruitAvailableText.text;
         __instance.RecruitAvailableText.text = OriginalRecruitText + " (" + GetClassName( recruit ) + ')';
      } catch ( Exception ex ) { Error( ex ); } }

      private static string GetClassName ( GeoUnitDescriptor recruit ) => recruit.Progression.MainSpecDef.ViewElementDef.DisplayName1.Localize();

      private static void AfterSetHaven_ShowResourceStock ( UIModuleSelectionInfoBox __instance, GeoSite ____site ) { try {
         var res = ____site.GetComponent<GeoHaven>()?.GetResourceTrading();
         var text = __instance.LeaderMottoText;
         if ( res?.Count > 0 && text != null ) {
            var conf = Mod.Config.Haven_Icons;
            text.text = string.Concat( res.Select( e =>
               string.Format( e.ResourceStock >= e.HavenOfferQuantity ? conf.In_Stock_Line : conf.Out_Of_Stock_Line,
               e.HavenReceiveQuantity, ResName( e.HavenWants ), e.HavenOfferQuantity, ResName( e.HavenOffers ), e.ResourceStock )
            ) ).Trim();
            text.fontStyle = FontStyle.Normal;
         } else
            text.fontStyle = FontStyle.Italic;
      } catch ( Exception ex ) { Error( ex ); } }

      private static string ResName ( ResourceType type ) {
         switch ( type ) {
            case ResourceType.Materials : return new LocalizedTextBind( "Geoscape/KEY_GEOSCAPE_MATERIALS" ).Localize();
            case ResourceType.Supplies : return new LocalizedTextBind( "Geoscape/KEY_GEOSCAPE_FOOD" ).Localize();
            case ResourceType.Tech : return new LocalizedTextBind( "Geoscape/KEY_GEOSCAPE_TECH" ).Localize();
         }
         return type.ToString();
      }

      private static void AfterGeoVisualAwake_MoveRecruitIcon ( GeoSiteVisualsController __instance ) { try {
         var npos = __instance.RecruitAvailableClassIcon?.gameObject?.transform?.position;
         if ( ! npos.HasValue ) return;
         var pos = npos.Value;
         pos.x += 0.06f;
         __instance.RecruitAvailableClassIcon.gameObject.transform.position = pos;
      } catch ( Exception ex ) { Error( ex ); } }

      private static void AfterHavenInfo_HideStickman ( GeoSiteVisualsController __instance, GeoSite site ) { try {
         var icon = __instance.RecruitAvailableIcon.gameObject;
         if ( ! icon.activeSelf ) return;
         icon.SetActive( false );
         //Api( "zy.ui.dump", __instance.gameObject );
      } catch ( Exception ex ) { Error( ex ); } }
   }
}
