﻿using Base.Utils.GameConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Sheepy.PhoenixPt.DebugConsole {
   public class Mod {
      public static void Init () => SplashMod();

      public static void SplashMod () {
         Application.logMessageReceived += UnityToConsole;
         GameConsoleWindow.DisableConsoleAccess = false;
      }

      public static void UnityToConsole ( string condition, string stackTrace, LogType type ) {
         GameConsoleWindow.Create().WriteLine( "{0} {1} {2}", type, condition, stackTrace );
      }
   }
}