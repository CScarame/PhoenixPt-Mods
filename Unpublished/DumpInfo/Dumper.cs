﻿using Base.Defs;
using Base.UI;
using PhoenixPoint.Common.Entities.Addons;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Equipments;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using static System.Reflection.BindingFlags;

namespace Sheepy.PhoenixPt.DumpInfo {

   internal class Dumper {
      private readonly Type DataType;
      private readonly List<BaseDef> Data;

      internal Dumper ( Type key, List<BaseDef> list ) {
         DataType = key;
         Data = list;
      }

      private StreamWriter Writer;
      private Dictionary< object, int > RecurringObject = new Dictionary< object, int >();

      internal void DumpData () { lock ( Data ) {
         ZyMod.Info( "Dumping {0} ({1})", DataType.Name, Data.Count );
         Data.Sort( CompareDef );
         var typeName = DataType.Name;
         var path = Path.Combine( Mod.ModDir, "Data-" + typeName + ".xml.gz" );
         File.Delete( path );
         using ( var fstream = new FileStream( path, FileMode.Create ) ) {
            //var buffer = new BufferedStream( fstream );
            var buffer = new GZipStream( fstream, CompressionLevel.Optimal );
            using ( var writer = new StreamWriter( buffer ) ) {
               Writer = writer;
               writer.Write( $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" );
               StartTag( typeName, "count", Data.Count.ToString(), false );
               if ( Mod.Query != null )
                  StartTag( "Game", "Version", Mod.Query( "Phoenix Point" )?.ToString(), true );
               StartTag( "DumpInfo", "Version", Assembly.GetExecutingAssembly().GetName().Version.ToString(), true );
               foreach ( var def in Data )
                  ToXml( def );
               writer.Write( $"</{typeName}>" );
               writer.Flush();
            }
         }
         //Info( "{0} dumped, {1} bytes", key.Name, new FileInfo( path ).Length );
         Data.Clear();
         RecurringObject.Clear();
      } }

      private static int CompareDef ( BaseDef a, BaseDef b ) {
         string aid = a.Guid, bid = b.Guid;
         if ( aid == null ) return bid == null ? 0 : -1;
         if ( bid == null ) return 1;
         return aid.CompareTo( bid );
      }

      private void ToXml ( object subject ) {
         if ( subject == null ) return;
         Mem2Xml( subject.GetType().Name, subject, 0 );
         Writer.Write( '\n' );
      }

      private void Mem2Xml ( string name, object val, int level ) {
         if ( val == null ) { NullMem( name ); return; }
         if ( val is string str ) { StartTag( name, "val", str, true ); return; }
         if ( val is LocalizedTextBind l10n ) {
            StartTag( name, "key", l10n.LocalizationKey, false );
            Writer.Write( EscXml( l10n.Localize() ) );
            EndTag( name );
            return;
         }
         if ( val is GameObject obj ) { StartTag( name, "name", obj.name, true ); return; }
         if ( val is byte[] ary ) {
            if ( name == "NativeData" ) // MapParcelDef.NativeData
               StartTag( name, "length", ary.Length.ToString(), true );
            else
               SimpleMem( name, Convert.ToBase64String( ary ) );
            return;
         }
         var type = val.GetType();
         if ( type.IsClass ) {
            if ( val is AK.Wwise.Bank ) return; // Ref error NullReferenceException
            if ( val is GeoFactionDef faction && DataType != typeof( GeoFactionDef ) ) { StartTag( name, "path", faction.ResourcePath, true ); return; }
            if ( val is TacticalActorDef tacChar && DataType != typeof( TacticalActorDef ) ) { StartTag( name, "path", tacChar.ResourcePath, true ); return; }
            if ( val is TacticalItemDef tacItem && DataType != typeof( TacticalItemDef ) ) { StartTag( name, "path", tacItem.ResourcePath, true ); return; }
            if ( type.Namespace?.StartsWith( "UnityEngine", StringComparison.InvariantCulture ) == true )
               { StartTag( name, "type", type.FullName, true ); return; }
            try {
               if ( RecurringObject.TryGetValue( val, out int link ) ) { StartTag( name, "ref", link.ToString( "X" ), true ); return; }
            } catch ( Exception ex ) { StartTag( name, "err_H", ex.GetType().Name, true ); return; } // Hash error
            var id = RecurringObject.Count;
            RecurringObject.Add( val, id );
            StartTag( name, "id", id.ToString( "X" ), false );
            if ( val is IEnumerable list && ! ( val is AddonDef ) ) {
               foreach ( var e in list )
                  if ( e == null )
                     NullMem( "LI" );
                  else
                     Mem2Xml( e.GetType() == type.GetElementType() ? "LI" : ( "LI." + e.GetType().Name ), e, level + 1 );
               EndTag( name );
               return;
            }
         } else {
            if ( type.IsPrimitive || type.IsEnum || val is Guid ) { StartTag( name, "val", val.ToString(), true ); return; }
            if ( val is Color color ) { WriteColour( name, color ); return; }
            StartTag( name ); // Other structs
         }
         Obj2Xml( val, level + 1 ); // Either structs or non-enum objects
         EndTag( name );
      }

      private void Obj2Xml ( object subject, int level ) {
         var type = subject.GetType();
         if ( level == 0 ) { Writer.Write( type.Name, subject, 1 ); return; }
         if ( level > 20 ) { Writer.Write( "..." ); return; }
         foreach ( var f in type.GetFields( Public | NonPublic | Instance ) ) try {
            Mem2Xml( f.Name, f.GetValue( subject ), level + 1 );
         } catch ( ApplicationException ex ) {
            StartTag( f.Name, "err_F", ex.GetType().Name, true ); // Field.GetValue error
         }
         if ( subject.GetType().IsClass ) {
            foreach ( var f in type.GetProperties( Public | NonPublic | Instance ) ) try {
               if ( f.GetCustomAttributes( typeof( ObsoleteAttribute ), false ).Any() ) continue;
               Mem2Xml( f.Name, f.GetValue( subject ), level + 1 );
            } catch ( ApplicationException ex ) {
               StartTag( f.Name, "err_P", ex.GetType().Name, true ); // Property.GetValue error
            }
         }
      }

      private void SimpleMem ( string name, string val ) {
         StartTag( name );
         Writer.Write( EscXml( val ) );
         EndTag( name );
      }

      private void WriteColour ( string tag, Color val ) {
         var w = Writer;
         w.Write( '<' );
         w.Write( EscTag( tag ) );
         w.Write( " r=\"" );
         w.Write( val.r );
         w.Write( "\" g=\"" );
         w.Write( val.g );
         w.Write( "\" b=\"" );
         w.Write( val.b );
         w.Write( "\" a=\"" );
         w.Write( val.a );
         w.Write( "\"/>" );
      }

      private void NullMem ( string name ) => StartTag( name, "null", "1", true );

      private void StartTag ( string name ) {
         var w = Writer;
         w.Write( '<' );
         w.Write( EscTag( name ) );
         w.Write( '>' );
      }

      private void StartTag ( string tag, string attr, string aVal, bool selfClose ) {
         var w = Writer;
         w.Write( '<' );
         w.Write( EscTag( tag ) );
         w.Write( ' ' );
         w.Write( attr );
         w.Write( "=\"" );
         w.Write( EscXml( aVal ) );
         w.Write( selfClose ? "\"/>" : "\">" );
      }
      
      private void EndTag ( string tag ) {
         var w = Writer;
         w.Write( "</" );
         w.Write( EscTag( tag ) );
         w.Write( '>' );
      }

      private static Regex cleanTag = new Regex( "[^\\w:-]+", RegexOptions.Compiled );
      private static string EscTag ( string txt ) {
         txt = cleanTag.Replace( txt, "." );
         while ( txt.Length > 0 && txt[0] == '.' ) txt = txt.Substring( 1 );
         return txt;
      }
      private static string EscXml ( string txt ) => SecurityElement.Escape( txt );
   }
}