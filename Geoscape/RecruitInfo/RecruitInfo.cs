﻿using Base.UI;
using Harmony;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewControllers.HavenDetails;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sheepy.PhoenixPt.RecruitInfo {
   public class Mod : ZyMod {
      internal static ModConfig Config;

      public void Init () => new Mod().MainMod();

      public void MainMod ( Func< string, object, object > api = null ) {
         SetApi( api );
         Patch( typeof( HavenFacilityItemController ), "SetRecruitmentGroup", null, "AfterSetRecruitment_ListPerks" );
      }

      private static GameObject CreateSkillText ( HavenFacilityItemController ctl ) {
         Transform owner = ctl.RecruitName?.transform?.parent?.parent;
         if ( owner == null ) throw new InvalidOperationException( "Recruit group not found, cannot add skill text" );
         GameObject skillObj = owner.Find( "PersonalSkills" )?.gameObject;

         if ( skillObj != null ) return skillObj;

         UnityEngine.UI.Text copyFrom = owner.Find( "RecruitCost" )?.Find( "Name" )?.GetComponent<UnityEngine.UI.Text>();
         if ( copyFrom == null ) copyFrom = ctl.RecruitName;
         skillObj = new GameObject( "PersonalSkills" );
         skillObj.transform.SetParent( owner );

         UnityEngine.UI.Text text = skillObj.AddComponent<UnityEngine.UI.Text>();
         text.font = copyFrom.font;
         text.fontStyle = copyFrom.fontStyle; // RecruitName Bold, Cost normal
         text.fontSize = copyFrom.fontSize; // RecruitName 52, Cost 42
         text.resizeTextForBestFit = true;
         text.resizeTextMinSize = 10;
         //Log.Info( "{0} {1} {2}", copyFrom.font, copyFrom.fontSize, copyFrom.fontStyle );

         Transform rect = skillObj.GetComponent<Transform>();
         //rect.localPosition = new Vector3( 200, 0, 0 );
         rect.localScale = new Vector3( 1, 1, 1 );

         UnityEngine.UI.Outline outline = skillObj.AddComponent<UnityEngine.UI.Outline>();
         outline.effectColor = Color.black;
         outline.effectDistance = new Vector2( 2, 2 );

         skillObj.AddComponent<UITooltipText>();
         skillObj.AddComponent<CanvasRenderer>();
         return skillObj;
      }

      private static string BuildHint ( LocalizedTextBind title, LocalizedTextBind desc ) {
         return "<b>" + title.Localize() + "</b>\n" + desc.Localize();
      }

      public static void AfterSetRecruitment_ListPerks ( HavenFacilityItemController __instance, GeoHaven ____haven ) { try {
         GameObject skillText = CreateSkillText( __instance );
         var recruit = ____haven?.AvailableRecruit;
         var tracks = recruit?.Progression?.AbilityTracks;
         if ( tracks == null ) {
            skillText.SetActive( false );
            return;
         }

         foreach ( var track in tracks ) {
            if ( track == null || track.Source != AbilityTrackSource.Personal ) continue;
            ViewElementDef mainClass = recruit.Progression.MainSpecDef.ViewElementDef;
            IEnumerable<ViewElementDef> abilities = track.AbilitiesByLevel.Where( e => e?.Ability != null ).Select( e => e.Ability.ViewElementDef );
            string list = abilities.Select( e => TitleCase( e.DisplayName1.Localize() ) ).Join();
            string hint = abilities.Select( e => BuildHint( e.DisplayName1, e.Description ) ).Join( null, "\n\n" );
            skillText.GetComponent<UnityEngine.UI.Text>().text = "     " + list;
            skillText.GetComponent<UITooltipText>().TipText = BuildHint( mainClass.DisplayName1, mainClass.DisplayName2 ) + "\n\n" + hint;
            skillText.SetActive( true );
            //skillText.GetComponent<UITooltipText>().UpdateText( hint );
            break;
         }

         //DumpComponent( __instance.gameObject );

      } catch ( Exception ex ) { Error( ex ); } }
      /*
      public static void DumpComponent ( GameObject root ) { DumpComponents( "", new HashSet<object>(), root ); }
      public static void DumpComponents ( string prefix, HashSet<object> logged, GameObject e ) {
         if ( prefix.Length > 20 ) return;
         if ( logged.Contains( e ) ) return;
         logged.Add( e );
         Log.Info( "{0}> {1} {2}{3} Layer {4}", prefix, e.name, e.GetType(), e.activeSelf ? "" : " (Inactive)", e.layer );
         foreach ( var c in e.GetComponents<Component>() ) {
            if ( c is UnityEngine.UI.Text txt )
               Log.Info( "{0}...{1} {2} {3}", prefix, c.GetType(), txt.color, txt.text );
            else if ( c is Transform rect )
               Log.Info( "{0}...{1} Pos {2} Scale {3} Rotate {4}", prefix, c.GetType(), rect.localPosition, rect.localScale, rect.localRotation );
            else if ( c is UnityEngine.UI.LayoutGroup layout )
               Log.Info( "{0}...{1} Padding {2}", prefix, c.GetType(), layout.padding );
            else
               Log.Info( "{0}...{1}", prefix, c.GetType() );
         }
         for ( int i = 0 ; i < e.transform.childCount ; i++ ) 
            DumpComponents( prefix + "  ", logged, e.transform.GetChild( i ).gameObject );
      }
      */
   }
}