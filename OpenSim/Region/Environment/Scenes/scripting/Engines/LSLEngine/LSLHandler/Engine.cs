/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
/* Original code: Tedd Hansen */
using System;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Threading;

    namespace OpenSim.Region.Scripting.LSL
    {

        
        public class Engine
        {
            //private string LSO_FileName = @"LSO\AdditionTest.lso";
            private string LSO_FileName;// = @"LSO\CloseToDefault.lso";
            AppDomain appDomain;            

            public void Start(string FileName)
            {
                LSO_FileName = FileName;


                                //appDomain = AppDomain.CreateDomain("AlternateAppDomain");
                appDomain = Thread.GetDomain();
                
                // Create Assembly Name
                AssemblyName asmName = new AssemblyName();
                asmName.Name = System.IO.Path.GetFileNameWithoutExtension(LSO_FileName);
                //asmName.Name = "TestAssembly";

                string DLL_FileName = asmName.Name + ".dll";
                string DLL_FileName_WithPath = System.IO.Path.GetDirectoryName(FileName) + @"\" + DLL_FileName;

                Common.SendToLog("LSO File Name: " + System.IO.Path.GetFileName(FileName));
                Common.SendToLog("Assembly name: " + asmName.Name);
                Common.SendToLog("Assembly File Name: " + asmName.Name + ".dll");
                Common.SendToLog("Starting processing of LSL ByteCode...");
                Common.SendToLog("");


                
                // Create Assembly
                AssemblyBuilder asmBuilder = appDomain.DefineDynamicAssembly(
                    asmName, 
                    AssemblyBuilderAccess.RunAndSave
                    );
                //// Create Assembly
                //AssemblyBuilder asmBuilder =
                //    Thread.GetDomain().DefineDynamicAssembly
                //(asmName, AssemblyBuilderAccess.RunAndSave);

                // Create a module (and save to disk)
                ModuleBuilder modBuilder = asmBuilder.DefineDynamicModule
                                (asmName.Name,
                                DLL_FileName);

                //Common.SendToDebug("asmName.Name is still \"" + asmName.Name + "\"");
                // Create a Class (/Type)
                TypeBuilder typeBuilder = modBuilder.DefineType(
                                        "LSL_ScriptObject",
                                        TypeAttributes.Public |  TypeAttributes.BeforeFieldInit);
                //,
                //                        typeof());
                //, typeof(LSL_BuiltIn_Commands_Interface));
                //,
                //                        typeof(object),
                //                        new Type[] { typeof(LSL_CLRInterface.LSLScript) });


                if (Common.IL_CreateConstructor)
                    IL_CREATE_CONSTRUCTOR(typeBuilder);
                

                /*
                 * Generate the IL itself
                 */

                LSO_Parser LSOP = new LSO_Parser();
                LSOP.ParseFile(LSO_FileName, typeBuilder);

                /*
                 * Done generating. Create a type and run it.
                 */



                Common.SendToLog("Attempting to compile assembly...");
                // Compile it
                Type type = typeBuilder.CreateType();
                Common.SendToLog("Compilation successful!");

                Common.SendToLog("Saving assembly: " + DLL_FileName);
                asmBuilder.Save(DLL_FileName);


                Common.SendToLog("Creating an instance of new assembly...");
                // Create an instance we can play with
                //LSLScript hello = (LSLScript)Activator.CreateInstance(type);
                //LSL_CLRInterface.LSLScript MyScript = (LSL_CLRInterface.LSLScript)Activator.CreateInstance(type);
                object MyScript = (object)Activator.CreateInstance(type);


                Common.SendToLog("");
            
            System.Reflection.MemberInfo[] Members = type.GetMembers();
                    
            Common.SendToLog("Members of assembly " + type.ToString () + ":");
            foreach (MemberInfo member in Members )
                Common.SendToLog(member.ToString());

                
                // Play with it
                //MyScript.event_state_entry("Test");
                object[] args = { null } ;
                //System.Collections.Generic.List<string> Functions = (System.Collections.Generic.List<string>)type.InvokeMember("GetFunctions", BindingFlags.InvokeMethod, null, MyScript, null);

                string[] ret = { };
                if (Common.IL_CreateFunctionList)
                    ret = (string[])type.InvokeMember("GetFunctions", BindingFlags.InvokeMethod, null, MyScript, null);

                foreach (string s in ret)
                {
                    Common.SendToLog("");
                    Common.SendToLog("*** Executing LSL Server Event: " + s);
                    //object test = type.GetMember(s);
                    //object runner = type.InvokeMember(s, BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance, null, MyScript, args);
                    //runner();
                    //objBooks_Late = type.InvokeMember(s, BindingFlags.CreateInstance, null, objApp_Late, null);
                    type.InvokeMember(s, BindingFlags.InvokeMethod, null, MyScript, new object[] { "Test" });
                    
                }
                
            }


            private static void IL_CREATE_CONSTRUCTOR(TypeBuilder typeBuilder)
            {


                Common.SendToDebug("IL_CREATE_CONSTRUCTOR()");
                //ConstructorBuilder constructor = typeBuilder.DefineConstructor(
                //            MethodAttributes.Public,
                //            CallingConventions.Standard, 
                //            new Type[0]);
                ConstructorBuilder constructor = typeBuilder.DefineConstructor(
                            MethodAttributes.Public |
                            MethodAttributes.SpecialName |
                            MethodAttributes.RTSpecialName,
                            CallingConventions.Standard,
                            new Type[0]);

                //Define the reflection ConstructorInfor for System.Object
                ConstructorInfo conObj = typeof(object).GetConstructor(new Type[0]);
     
                //call constructor of base object
                ILGenerator il = constructor.GetILGenerator();

                Common.SendToDebug("IL_CREATE_CONSTRUCTOR: Creating global: UInt32 State = 0;");
                string FieldName;
                // Create state object
                FieldName = "State";
                FieldBuilder State_fb = typeBuilder.DefineField(
                    FieldName, 
                    typeof(UInt32), 
                    FieldAttributes.Public);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, 0);
                il.Emit(OpCodes.Stfld, State_fb);


                Common.SendToDebug("IL_CREATE_CONSTRUCTOR: Creating global: LSL_BuiltIn_Commands_TestImplementation LSL_BuiltIns = New LSL_BuiltIn_Commands_TestImplementation();");
                //Type objType1 = typeof(object);
                Type objType1 = typeof(LSL_BuiltIn_Commands_TestImplementation);

                FieldName = "LSL_BuiltIns";
                FieldBuilder LSL_BuiltIns_fb = typeBuilder.DefineField(
                    FieldName, 
                    objType1, 
                    FieldAttributes.Public);

                //LSL_BuiltIn_Commands_TestImplementation _ti = new LSL_BuiltIn_Commands_TestImplementation();
                il.Emit(OpCodes.Ldarg_0);
                //il.Emit(OpCodes.Ldstr, "Test 123");
                il.Emit(OpCodes.Newobj, objType1.GetConstructor(new Type[] { }));
                il.Emit(OpCodes.Stfld, LSL_BuiltIns_fb);

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, conObj);



                ////il.Emit(OpCodes.Newobj, typeof(UInt32));
                //il.Emit(OpCodes.Starg_0);
                //// Create LSL function library
                //FieldBuilder LSL_BuiltIns_fb = typeBuilder.DefineField("LSL_BuiltIns", typeof(LSL_BuiltIn_Commands_Interface), FieldAttributes.Public);
                //il.Emit(OpCodes.Newobj, typeof(LSL_BuiltIn_Commands_Interface));
                //il.Emit(OpCodes.Stloc_1);

                il.Emit(OpCodes.Ret);
            }
        }
    }
