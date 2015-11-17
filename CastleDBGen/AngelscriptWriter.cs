﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastleDBGen
{
    internal class AngelscriptWriter : BaseDBWriter
    {
        static readonly string ASClassStart = "\r\nclass {0} {{\r\n"; // class SHEETNAME
        static readonly string ASClassStartInherit = "\r\nclass {0} : {1} {{\r\n"; // class SHEETNAME
        static readonly string ASClassEnd = "};\r\n";
        static readonly string ASProperty = "{2}{0} {1};\r\n"; // Type name;\n

        public override void WriteClassDefinitions(CastleDB database, string fileBase, string sourceFileName, Dictionary<string, string> switches, List<string> errors)
        {
            Dictionary<string, Dictionary<string, string>> IDSTable = new Dictionary<string, Dictionary<string, string>>();
            int tabDepth = 0;
            string fileText = string.Format("// AUTOGENERATED ANGELSCRIPT SOURCE CODE FROM {0}\r\n", sourceFileName);
            string dbName = "GameDatabase";
            if (switches.ContainsKey("db"))
                dbName = switches["db"];
            
            bool integerIDs = false;
            if (switches.ContainsKey("id"))
                integerIDs = switches["id"].Equals("int");
            
            bool binIO = false;
            bool jsonOff = false;
            if (switches.ContainsKey("bin"))
            {
                binIO = switches["bin"].Equals("on") || switches["bin"].Equals("only");
                jsonOff = switches["bin"].Equals("only");
            }

            string inherit = "";
            if (switches.ContainsKey("inherit"))
                inherit = switches["inherit"];

            // Angelscript namespace need to go externally
            // Scan for enumerations and flags
            foreach (CastleSheet sheet in database.Sheets)
            {
                IDSTable[sheet.Name] = new Dictionary<string, string>();
                foreach (CastleColumn column in sheet.Columns)
                {
                    if (column.TypeID == CastleType.Enum)
                    {
                        fileText += string.Format("\r\nenum E_{0} {{\r\n", column.Name.ToUpper());
                        foreach (string value in column.Enumerations)
                            fileText += string.Format("{0}{1},\r\n", GetTabString(tabDepth + 0), value.ToUpper());
                        fileText += "}\r\n";
                    }
                    else if (column.TypeID == CastleType.Flags)
                    {
                        fileText += "\r\n";
                        int index = 0;
                        foreach (string value in column.Enumerations)
                        {
                            fileText += string.Format("const uint {0}_{1} = {2};\r\n", column.Name.ToUpper(), value.ToUpper(), 1 << index);
                            ++index;
                        }
                    }
                }
            }

            if (switches.ContainsKey("ns"))
            {
                fileText += string.Format("\r\nnamespace {0} {{\r\n", switches["ns"]);
            }

            foreach (CastleSheet sheet in database.Sheets)
            {
                string sheetName = sheet.Name.Replace('@', '_');
                string classStr = inherit.Length > 0 ? String.Format(ASClassStartInherit, sheetName, inherit) : String.Format(ASClassStart, sheetName);

                foreach (CastleColumn column in sheet.Columns)
                {
                    switch (column.TypeID)
                    {
                    case CastleType.UniqueIdentifier:
                        classStr += String.Format(ASProperty, "String", column.Name, GetTabString(tabDepth + 0));
                        break;
                    case CastleType.Boolean:
                        classStr += String.Format(ASProperty, "bool", column.Name, GetTabString(tabDepth + 0));
                        break;
                    case CastleType.Color:
                        classStr += String.Format(ASProperty, "Color", column.Name, GetTabString(tabDepth + 0));
                        break;
                    case CastleType.Custom:
                    case CastleType.Dynamic:
                        errors.Add(String.Format("Sheet {0}, type {1} unsupported", column.Name, column.TypeID.ToString()));
                        break;
                    case CastleType.Enum:
                        classStr += String.Format(ASProperty, "int", column.Name, GetTabString(tabDepth + 0));
                        break;
                    case CastleType.File:
                        classStr += String.Format(ASProperty, "String", column.Name, GetTabString(tabDepth + 0));
                        break;
                    case CastleType.Flags:
                        classStr += String.Format(ASProperty, "uint", column.Name, GetTabString(tabDepth + 0));
                        break;
                    case CastleType.Image:
                        errors.Add(String.Format("Sheet {0}, type {1} unsupported", column.Name, column.TypeID.ToString()));
                        break;
                    case CastleType.Integer:
                        classStr += String.Format(ASProperty, "int", column.Name, GetTabString(tabDepth + 0));
                        break;
                    case CastleType.Layer:
                        errors.Add(String.Format("Sheet {0}, type {1} unsupported", column.Name, column.TypeID.ToString()));
                        break;
                    case CastleType.List:
                        classStr += String.Format("{0}Array<{1}@> {2};\r\n", GetTabString(tabDepth + 0), String.Format("{0}_{1}", sheet.Name, column.Name), column.Name);
                        break;
                    case CastleType.Ref:
                        classStr += String.Format("{0}{1}@ {2};\r\n", GetTabString(tabDepth + 0), column.Key, column.Name);
                        classStr += String.Format("{0}private String {1}Key;\r\n", GetTabString(tabDepth + 0), column.Name);
                        break;
                    case CastleType.Text:
                        classStr += String.Format(ASProperty, "String", column.Name, GetTabString(tabDepth + 0));
                        break;
                    case CastleType.TileLayer:
                        errors.Add(String.Format("Sheet {0}, type {1} unsupported", column.Name, column.TypeID.ToString()));
                        break;
                    case CastleType.TilePos:
                        errors.Add(String.Format("Sheet {0}, type {1} unsupported", column.Name, column.TypeID.ToString()));
                        break;
                    }
                }
            // generate destructor, clears the lists and refs
                classStr += string.Format("\r\n{0}~{1}() {{\r\n", GetTabString(tabDepth + 0), sheetName);
                foreach (CastleColumn col in sheet.Columns)
                {
                    if (col.TypeID == CastleType.List)
                        classStr += string.Format("{0}{1}.Clear();\r\n", GetTabString(tabDepth + 1), col.Name);
                    else if (col.TypeID == CastleType.Ref)
                        classStr += string.Format("{0}@{1} = null;\r\n", GetTabString(tabDepth + 1), col.Name);
                }
                classStr += string.Format("{0}}}\r\n", GetTabString(tabDepth + 0));

            // generate loading function
                if (!jsonOff)
                    classStr += string.Format("\r\n{0}void Load(JSONValue&in value) {{\r\n", GetTabString(tabDepth + 0));
                string loadBinStr = string.Format("\r\n{0}void Load(Deserializer&in source) {{\r\n", GetTabString(tabDepth + 0));
                string saveBinStr = string.Format("\r\n{0}void Save(Serializer&in dest) {{\r\n", GetTabString(tabDepth + 0));
                foreach (CastleColumn col in sheet.Columns)
                {
                    switch (col.TypeID)
                    {
                        case CastleType.UniqueIdentifier:
                            if (!jsonOff)
                                classStr += string.Format("{0}{1} = value[\"{1}\"].GetString();\r\n", GetTabString(tabDepth + 1), col.Name);
                            loadBinStr += string.Format("{0}{1} = source.ReadString();\r\n", GetTabString(tabDepth + 1), col.Name);
                            saveBinStr += string.Format("{0}dest.WriteString({1});\r\n", GetTabString(tabDepth + 1), col.Name);
                            break;
                        case CastleType.Boolean:
                            if (!jsonOff)
                                classStr += string.Format("{0}{1} = value[\"{1}\"].GetBool();\r\n", GetTabString(tabDepth + 1), col.Name);
                            loadBinStr += string.Format("{0}{1} = source.ReadBool();\r\n", GetTabString(tabDepth + 1), col.Name);
                            saveBinStr += string.Format("{0}dest.WriteBool({1});\r\n", GetTabString(tabDepth + 1), col.Name);
                            break;
                        case CastleType.Color:
                            if (!jsonOff)
                                classStr += string.Format("{0}{1}.FromUInt(value[\"{1}\"].GetUInt());\r\n", GetTabString(tabDepth + 1), col.Name);
                            loadBinStr += string.Format("{0}{1} = source.ReadString();\r\n", GetTabString(tabDepth + 1), col.Name);
                            saveBinStr += string.Format("{0}dest.WriteColor({1});\r\n", GetTabString(tabDepth + 1), col.Name);
                            break;
                        case CastleType.Enum:
//TODO! With every angelscript update check for enums updated to accept int
                            if (!jsonOff)
                                classStr += string.Format("{0}{1} = value[\"{1}\"].GetInt();\r\n", GetTabString(tabDepth + 1), col.Name);
                            loadBinStr += string.Format("{0}{1} = source.ReadInt();\r\n", GetTabString(tabDepth + 1), col.Name);
                            saveBinStr += string.Format("{0}dest.WriteInt({1});\r\n", GetTabString(tabDepth + 1), col.Name);
                            break;

                        case CastleType.Image:
                            break;
                        case CastleType.File:
                            if (!jsonOff)
                                classStr += string.Format("{0}{1} = value[\"{1}\"].GetString();\r\n", GetTabString(tabDepth + 1), col.Name);
                            loadBinStr += string.Format("{0}{1} = source.ReadString();\r\n", GetTabString(tabDepth + 1), col.Name);
                            saveBinStr += string.Format("{0}dest.WriteString({1});\r\n", GetTabString(tabDepth + 1), col.Name);
                            break;
                        case CastleType.Flags:
                            if (!jsonOff)
                                classStr += string.Format("{0}{1} = value[\"{1}\"].GetUInt();\r\n", GetTabString(tabDepth + 1), col.Name);
                            loadBinStr += string.Format("{0}{1} = source.ReadUInt();\r\n", GetTabString(tabDepth + 1), col.Name);
                            saveBinStr += string.Format("{0}dest.WriteUInt({1});\r\n", GetTabString(tabDepth + 1), col.Name);
                            break;
                        case CastleType.Float:
                            if (!jsonOff)
                                classStr += string.Format("{0}{1} = value[\"{1}\"].GetFloat();\r\n", GetTabString(tabDepth + 1), col.Name);
                            loadBinStr += string.Format("{0}{1} = source.ReadFloat();\r\n", GetTabString(tabDepth + 1), col.Name);
                            saveBinStr += string.Format("{0}dest.WriteFloat({1});\r\n", GetTabString(tabDepth + 1), col.Name);
                            break;
                        case CastleType.Integer:
                            if (!jsonOff)
                                classStr += string.Format("{0}{1} = value[\"{1}\"].GetInt();\r\n", GetTabString(tabDepth + 1), col.Name);
                            loadBinStr += string.Format("{0}{1} = source.ReadInt();\r\n", GetTabString(tabDepth + 1), col.Name);
                            saveBinStr += string.Format("{0}dest.WriteInt({1});\r\n", GetTabString(tabDepth + 1), col.Name);
                            break;
                        case CastleType.List:
                            if (!jsonOff)
                            {
                                classStr += string.Format("{0}JSONValue {1}Array = value[\"{1}\"];\r\n", GetTabString(tabDepth + 1), col.Name);
                                classStr += string.Format("{0}for (uint i = 0; i < {1}Array.size; ++i) {{\r\n", GetTabString(tabDepth + 1), col.Name);
                                classStr += string.Format("{0}{1}@ val = {1}();\r\n", GetTabString(tabDepth + 2), string.Format("{0}_{1}", sheet.Name, col.Name));
                                classStr += string.Format("{0}val.Load({1}Array[i]);\r\n{0}{2}.Push(val);\r\n", GetTabString(tabDepth + 2), col.Name, col.Name);
                                classStr += string.Format("{0}}} \r\n", GetTabString(tabDepth + 1));
                            }

                            loadBinStr += string.Format("{0}uint {1}Ct = source.ReadUInt();\r\n{0}for (uint i = 0; i < {1}Ct; ++i) {{\r\n", GetTabString(tabDepth + 1), col.Name);
                            loadBinStr += string.Format("{0}{1}@ val = {1}();\r\n", GetTabString(tabDepth + 2), string.Format("{0}_{1}", sheet.Name, col.Name));
                            loadBinStr += string.Format("{0}val.Load(source);\r\n{0}{2}.Push(val);\r\n", GetTabString(tabDepth + 2), col.Name, col.Name);
                            loadBinStr += string.Format("{0}}} \r\n", GetTabString(tabDepth + 1));

                            saveBinStr += string.Format("{0}dest.WriteUInt({1}.length);\r\n", GetTabString(tabDepth + 1), col.Name);
                            saveBinStr += string.Format("{0}for (uint i = 0; i < {1}.length; ++i)\r\n", GetTabString(tabDepth + 1), col.Name);
                            saveBinStr += string.Format("{0}{1}[i].Save(dest);\r\n", GetTabString(tabDepth + 2), col.Name);

                            break;
                        case CastleType.Ref:
                            if (!jsonOff)
                                classStr += string.Format("{0}{1}Key = value[\"{1}\"].GetString();\r\n", GetTabString(tabDepth + 1), col.Name);
                            loadBinStr += string.Format("{0}{1}Key = source.ReadString();\r\n", GetTabString(tabDepth + 1), col.Name);
                            saveBinStr += string.Format("{0}if ({2} is null)\r\n{1}source.WriteString(\"\");\r\n{0}else\r\n{1}source.WriteString({2}.{3});\r\n", GetTabString(tabDepth + 1), GetTabString(tabDepth + 2), col.Name, database.Sheets.FirstOrDefault(s => s.Name.Equals(col.Key)).IDColumn.Name);
                            break;
                        case CastleType.Text:
                            if (!jsonOff)
                                classStr += string.Format("{0}{1} = value[\"{1}\"].GetString();\r\n", GetTabString(tabDepth + 1), col.Name);
                            loadBinStr += string.Format("{0}{1} = source.ReadString();\r\n", GetTabString(tabDepth + 1), col.Name);
                            break;
                    }
                }
                if (!jsonOff)
                    classStr += string.Format("{0}}}\r\n", GetTabString(tabDepth + 0));

                if (binIO)
                {
                    loadBinStr += string.Format("{0}}}\r\n", GetTabString(tabDepth + 0));
                    saveBinStr += string.Format("{0}}}\r\n", GetTabString(tabDepth + 0));
                    classStr += loadBinStr;
                    classStr += saveBinStr;
                }

                classStr += string.Format("\r\n{0}void ResolveReferences({1}@ db) {{\r\n", GetTabString(tabDepth + 0), dbName);
                foreach (CastleColumn col in sheet.Columns)
                {
                    if (col.TypeID == CastleType.Ref)
                    {
                        classStr += string.Format("{0}for (uint i = 0; i < db.{1}List.length; ++i) {{\r\n", GetTabString(tabDepth + 1), col.Key);
                        classStr += string.Format("{0}if (db.{1}List[i].{2} == {3}) {{\r\n", GetTabString(tabDepth + 2), col.Key, database.Sheets.FirstOrDefault(s => s.Name.Equals(col.Key)).GetKeyName(), String.Format("{0}Key", col.Name));
                        classStr += string.Format("{0}@{1} = @db.{2}List[i];\r\n", GetTabString(tabDepth + 3), col.Name, col.Key);
                        classStr += string.Format("{0}break;\r\n", GetTabString(tabDepth + 3));
                        classStr += string.Format("{0}}}\r\n", GetTabString(tabDepth + 2));
                        classStr += string.Format("{0}}}\r\n", GetTabString(tabDepth + 1));
                    }
                }
                classStr += string.Format("{0}}}\r\n", GetTabString(tabDepth + 0));

                classStr += ASClassEnd;
                fileText += classStr;
            }

    // Generate the database class
            if (inherit.Length == 0)
                fileText += String.Format("\r\nclass {0} {{\r\n", dbName);
            else
                fileText += String.Format("\r\nclass {0} : {1} {{\r\n", dbName, inherit);
            foreach (CastleSheet sheet in database.Sheets)
            {
                if (sheet.Name.Contains("@"))
                    continue;
                fileText += string.Format("{0}Array<{1}@> {1}List;\r\n", GetTabString(tabDepth + 0), sheet.Name.Replace("@","_"));
            }

            fileText += string.Format("\r\n{0}~{1}() {{\r\n", GetTabString(tabDepth + 0), dbName);
            foreach (CastleSheet sheet in database.Sheets)
            {
                if (sheet.Name.Contains("@"))
                    continue;
                fileText += string.Format("{0}{1}List.Clear();\r\n", GetTabString(tabDepth + 1), sheet.Name.Replace("@","_"));
            }
            fileText += string.Format("{0}}}\r\n", GetTabString(tabDepth + 0));

        // Database load
            if (!jsonOff)
            {
                fileText += string.Format("\r\n{0}void Load(JSONFile@ file) {{\r\n", GetTabString(tabDepth + 0));
                fileText += string.Format("{0}JSONValue sheetsElem = file.GetRoot()[\"sheets\"];\r\n", GetTabString(tabDepth + 1));
                fileText += string.Format("{0}for (uint i = 0; i < sheetsElem.size; ++i) {{\r\n", GetTabString(tabDepth + 1));
                fileText += string.Format("{0}JSONValue sheet = sheetsElem[i];\r\n{0}String sheetName = sheet[\"name\"].GetString();\r\n", GetTabString(tabDepth + 2));
                bool first = true;
                foreach (CastleSheet sheet in database.Sheets)
                {
                    if (sheet.Name.Contains("@"))
                        continue;
                    fileText += string.Format("{0}{2} (sheetName == \"{1}\") {{\r\n", GetTabString(tabDepth + 2), sheet.Name, first ? "if" : "else if");
                    fileText += string.Format("{0}JSONValue linesElem = sheet[\"lines\"];\r\n", GetTabString(tabDepth + 3));
                    fileText += string.Format("{0}for (uint j = 0; j < linesElem.size; ++j) {{\r\n", GetTabString(tabDepth + 3));
                    fileText += string.Format("{0}{1}@ val = {1}();\r\n{0}val.Load(linesElem[j]);\r\n{0}{1}List.Push(val);\r\n", GetTabString(tabDepth + 4), sheet.Name);
                    fileText += string.Format("{0}}}\r\n", GetTabString(tabDepth + 3));
                    fileText += string.Format("{0}}}\r\n", GetTabString(tabDepth + 2));
                    first = false;
                }
                fileText += string.Format("{0}}}\r\n", GetTabString(tabDepth + 1));
                // Write reference resolving code
                foreach (CastleSheet sheet in database.Sheets)
                {
                    if (sheet.HasReferences())
                    {
                        fileText += string.Format("{0}for (uint i = 0; i < {1}List.length; ++i)\r\n", GetTabString(tabDepth + 1), sheet.Name);
                        fileText += string.Format("{0}{1}List[i].ResolveReferences(this);\r\n", GetTabString(tabDepth + 2), sheet.Name);
                    }
                }
                fileText += string.Format("{0}}}\r\n", GetTabString(tabDepth + 0));
            }

            if (binIO)
            {
                fileText += string.Format("\r\n{0}void Load(Deserializer& source) {{\r\n", GetTabString(tabDepth + 0), dbName);
                foreach (CastleSheet sheet in database.Sheets)
                {
                    if (sheet.Name.Contains("@"))
                        continue;
                    fileText += string.Format("{0}uint {1}Ct = source.ReadUInt();\r\n", GetTabString(tabDepth + 1), sheet.Name);
                    fileText += string.Format("{0}for (uint i = 0; i < {1}Ct; ++i) {{\r\n{2}{1}@ val = {1}();\r\n{2}{1}.Load(source);\r\n{2}{1}List.Push(val);\r\n{0}}}\r\n", GetTabString(tabDepth + 1), sheet.Name, GetTabString(tabDepth + 2));
                }
                // Write reference resolving code
                foreach (CastleSheet sheet in database.Sheets)
                {
                    if (sheet.HasReferences())
                    {
                        fileText += string.Format("{0}for (uint i = 0; i < {1}List.length; ++i)\r\n", GetTabString(tabDepth + 1), sheet.Name);
                        fileText += string.Format("{0}{1}List[i].ResolveReferences(this);\r\n", GetTabString(tabDepth + 2), sheet.Name);
                    }
                }
                fileText += string.Format("{0}}}\r\n", GetTabString(tabDepth + 0));

                fileText += string.Format("\r\n{0}void Save(Serializer& dest) {{\r\n", GetTabString(tabDepth + 0), dbName);
                foreach (CastleSheet sheet in database.Sheets)
                {
                    if (sheet.Name.Contains("@"))
                        continue;
                    fileText += string.Format("{0}dest.WriteUInt({1}List.length);\r\n", GetTabString(tabDepth + 1), sheet.Name);
                    fileText += string.Format("{0}for (uint i = 0; i < {1}List.length; ++i)\r\n{2}{1}List[i].Save(dest);\r\n", GetTabString(tabDepth + 1), sheet.Name, GetTabString(tabDepth + 2));
                }
                fileText += string.Format("{0}}}\r\n", GetTabString(tabDepth + 0));
            }

            fileText += "}\r\n";

            if (switches.ContainsKey("ns"))
                fileText += "\r\n}\r\n";
            System.IO.File.WriteAllText(fileBase, fileText);
        }
    }
}
