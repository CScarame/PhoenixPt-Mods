﻿using System;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.PhoenixPt.GlobeTweaks {

   internal class ModConfig {
      public bool Base_Centre_On_Heal = true;
      public bool Base_Pause_On_Heal = true;
      public bool Center_On_New_Base = true;
      public bool No_Auto_Unpause = true;
      public bool Notice_On_HP_Only_Heal = false;
      public bool Notice_On_Stamina_Only_Heal = true;
      public bool Vehicle_Centre_On_Heal = true;
      public bool Vehicle_Pause_On_Heal = true;
      public HavenIconConfig Haven_Icons = new HavenIconConfig();
      public int  Config_Version = 20200419;

      internal void Upgrade () {
         if ( Config_Version < 20200419 ) {
            Config_Version = 20200419;
            ZyMod.Api( "config save", this );
         }
      }
   }

   internal class HavenIconConfig {
      public bool Always_Show_Recruit = true;
      public bool Always_Show_Soldier = true;
      public bool Always_Show_Trade   = true;
      public bool Show_Recruit_Class_In_Mouseover = true;
   }

   public class Mod : ZyAdvMod {

      internal static ModConfig Config;

      public static void Init () => new Mod().MainMod();

      public void MainMod ( Func< string, object, object > api = null ) {
         SetApi( api, out Config );
         new PauseModule().DoPatches();
         new GlyphModule().DoPatches();
      }

   }
}
