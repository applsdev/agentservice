/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using log4net;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web;
using System.Net;
using System.IO;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Services.Connectors.Inventory
{
    public class ASInventoryServiceConnector : ISessionAuthInventoryService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<string, InventoryServicesConnector> m_connectors = new Dictionary<string, InventoryServicesConnector>();

        public ASInventoryServiceConnector(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {

                IConfig inventoryConfig = source.Configs["InventoryService"];
                if (inventoryConfig == null)
                {
                    m_log.Error("[AS INVENTORY SERVICE]: InventoryService missing from OpenSim.ini");
                    return;
                }

                m_log.Info("[AS INVENTORY SERVICE]: AS inventory service enabled");
            }
        }

        private bool StringToUrlAndUserID(string id, out string url, out string userID)
        {
            url = String.Empty;
            userID = String.Empty;

            Uri assetUri;

            if (Uri.TryCreate(id, UriKind.Absolute, out assetUri) &&
                    assetUri.Scheme == Uri.UriSchemeHttp)
            {
                url = "http://" + assetUri.Authority;
                userID = assetUri.LocalPath.Trim(new char[] { '/' });
                return true;
            }

            return false;
        }
        private ISessionAuthInventoryService GetConnector(string url)
        {
            InventoryServicesConnector connector = null;
            lock (m_connectors)
            {
                if (m_connectors.ContainsKey(url))
                {
                    connector = m_connectors[url];
                }
                else
                {
                    // We're instantiating this class explicitly, but this won't
                    // work in general, because the remote grid may be running
                    // an inventory server that has a different protocol.
                    // Eventually we will want a piece of protocol asking
                    // the remote server about its kind. Definitely cool thing to do!
                    connector = new InventoryServicesConnector(url);
                    m_connectors.Add(url, connector);
                }
            }
            return connector;
        }

        public string Host
        {
            get { return string.Empty; }
        }

        public void GetUserInventory(string id, UUID sessionID, InventoryReceiptCallback callback)
        {
            m_log.Debug("[ASInventory]: GetUserInventory DEPRECATED" + id);
            
            // we shouldn't need this as the inventory is fetched direct from the AS
            
            //string url = string.Empty;
            //string userID = string.Empty;

            //if (StringToUrlAndUserID(id, out url, out userID))
            //{
            //    ISessionAuthInventoryService connector = GetConnector(url);
            //    connector.GetUserInventory(userID, sessionID, callback);
            //}

        }

        /// <summary>
        /// Gets the user folder for the given folder-type
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public Dictionary<AssetType, InventoryFolderBase> GetSystemFolders(string id, UUID sessionID)
        {
            m_log.Debug("[ASInventory]: GetSystemFolders " + id);
            string url = string.Empty;
            string userID = string.Empty;

            if (StringToUrlAndUserID(id, out url, out userID))
            {
                ISessionAuthInventoryService connector = GetConnector(url);
                return connector.GetSystemFolders(userID, sessionID);
            }

            return new Dictionary<AssetType, InventoryFolderBase>();
        }

        /// <summary>
        /// Gets everything (folders and items) inside a folder
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="folderID"></param>
        /// <returns></returns>
        public InventoryCollection GetFolderContent(string id, UUID folderID, UUID sessionID)
        {
            m_log.Debug("[ASInventory]: GetFolderContent " + id);
            string url = string.Empty;
            string userID = string.Empty;

            if (StringToUrlAndUserID(id, out url, out userID))
            {
                ISessionAuthInventoryService connector = GetConnector(url);
                return connector.GetFolderContent(userID, folderID, sessionID);
            }

            return null;
        }

        public bool AddFolder(string uri, InventoryFolderBase folder, UUID sessionID)
        {
            m_log.Debug("[ASInventory]: AddFolder " + folder.ID);
            
            try
                {
                    OSDMap requestMap = convertInventoryFolderToOSD(folder);
                    SendRequest(requestMap, uri);
                }
                catch (WebException e)
                {
                    m_log.ErrorFormat("[ASInventory]: CreateInventoryFolder operation failed, {0} {1}",
                         e.Source, e.Message);
                    return false;
                }
                
            return true;
        }

        public bool UpdateFolder(string uri, InventoryFolderBase folder, UUID sessionID)
        {
            m_log.Debug("[ASInventory]: UpdateFolder " + folder.ID);
            
            try
                {
                    OSDMap requestMap = convertInventoryFolderToOSD(folder);
                    SendRequest(requestMap, uri);
                }
                catch (WebException e)
                {
                    m_log.ErrorFormat("[ASInventory]: UpdateFolder operation failed, {0} {1}",
                         e.Source, e.Message);
                    return false;
                }
                
            return true;
        }

        public bool MoveFolder(string uri, InventoryFolderBase folder, UUID sessionID)
        {
            m_log.Debug("[ASInventory]: MoveFolder " + folder.ID);
            
            try
            {
                OSDMap requestMap = convertInventoryFolderToOSD(folder);
                SendRequest(requestMap, uri);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[ASInventory]: MoveFolder operation failed, {0} {1}",
                                  e.Source, e.Message);
                return false;
            }
                
            return true;
        }

        public bool DeleteFolders(string uri, List<UUID> folders, UUID sessionID)
        {
            m_log.Debug("[ASInventory: DeleteFolders ");
            
            foreach(UUID folderID in folders)
            {
                InventoryFolderBase folder = new InventoryFolderBase();
                folder.ID = folderID;
                try
                {
                    OSDMap requestMap = convertInventoryFolderToOSD(folder);
                    SendRequest(requestMap, uri);
                }
                catch (WebException e)
                {
                    m_log.ErrorFormat("[ASInventory]: DeleteFolders operation failed, {0} {1}",
                                      e.Source, e.Message);
                    return false;
                }
            }
            
            return true;
        }

        public bool PurgeFolder(string uri, InventoryFolderBase folder, UUID sessionID)
        {
            m_log.Debug("[ASInventory: PurgeFolder ");
            try
            {
                OSDMap requestMap = convertInventoryFolderToOSD(folder);
                SendRequest(requestMap, uri);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[ASInventory]: DeleteFolders operation failed, {0} {1}",
                                  e.Source, e.Message);
                return false;
            }
            return true;
        }

        public List<InventoryItemBase> GetFolderItems(string id, UUID folderID, UUID sessionID)
        {
            string url = string.Empty;
            string userID = string.Empty;

            if (StringToUrlAndUserID(id, out url, out userID))
            {
                ISessionAuthInventoryService connector = GetConnector(url);
                return connector.GetFolderItems(userID, folderID, sessionID);
            }
            return new List<InventoryItemBase>();
        }
        
        public bool AddItem(string uri, InventoryItemBase item, UUID sessionID)
        {
            m_log.Debug("[ASInventory]: AddItem " + item.Name);
            try
            {
                OSDMap requestMap = convertInventoryItemToOSD(item);
                SendRequest(requestMap, uri);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[ASInventory]: Add new inventory item operation failed, {0} {1}",
                                  e.Source, e.Message);
                return false;
            }
            
            return true;
        }

        public bool UpdateItem(string uri, InventoryItemBase item, UUID sessionID)
        {
            m_log.Debug("[ASInventory]: UpdateItem " + item.Name);
            try
            {
                OSDMap requestMap = convertInventoryItemToOSD(item);
                SendRequest(requestMap, uri);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[ASInventory]: Update inventory item operation failed, {0} {1}",
                                  e.Source, e.Message);
                return false;
            }
            
            return true;
        }

        public bool MoveItems(string uri, List<InventoryItemBase> items, UUID sessionID)
        {
            m_log.Debug("[ASInventory]: MoveItems " + items.Count);
            
            foreach (InventoryItemBase item in items) // Loop through List with foreach
            {
         
                try
                {
                    OSDMap requestMap = convertInventoryItemToOSD(item);
                    SendRequest(requestMap, uri);
                }
                catch (WebException e)
                {
                    m_log.ErrorFormat("[ASInventory]: MoveInventoryItem operation failed, {0} {1}",
                         e.Source, e.Message);
                         
                    return false;
                }
            }
            
            return true;
        }

        public bool DeleteItems(string uri, List<UUID> itemIDs, UUID sessionID)
        {
            m_log.Debug("[ASInventory]: DeleteItems " + itemIDs.Count);
            
            InventoryItemBase item= new InventoryItemBase();
            foreach (UUID itemID in itemIDs) // Loop through List with foreach
            {
         
                try
                {
                    item.ID = itemID; 
                    OSDMap requestMap = convertInventoryItemToOSD(item);
                    SendRequest(requestMap, uri);
                }
                catch (WebException e)
                {
                    m_log.ErrorFormat("[ASInventory]: DeleteItems operation failed, {0} {1}",
                         e.Source, e.Message);
                         
                    return false;
                }
            }
            
            return true;
        }

        public InventoryItemBase QueryItem(string uri, InventoryItemBase item, UUID sessionID)
        {
            m_log.Debug("[ASInventory]: QueryItem " + item.Name);
            try
            {
                OSDMap requestMap = convertInventoryItemToOSD(item);
                OSDMap response = SendRequest(requestMap, uri);
                
                if(response!=null)
                    return OSDToInventoryItemBase(response);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[ASInventory]: QueryItem operation failed, {0} {1}",
                                  e.Source, e.Message);
            }
            
            return null;
        }

        public InventoryFolderBase QueryFolder(string uri, InventoryFolderBase folder, UUID sessionID)
        {
            m_log.Debug("[ASInventory]: QueryFolder " + folder.Name);
            try
            {
                OSDMap requestMap = convertInventoryFolderToOSD(folder);
                OSDMap response = SendRequest(requestMap, uri);
                
                if(response!=null)
                    return OSDToInventoryFolderBase(response);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[ASInventory]: QueryFolder operation failed, {0} {1}",
                                  e.Source, e.Message);
            }
            
            return null;
        }

        public int GetAssetPermissions(string id, UUID assetID, UUID sessionID)
        {
            string url = string.Empty;
            string userID = string.Empty;

            if (StringToUrlAndUserID(id, out url, out userID))
            {
                ISessionAuthInventoryService connector = GetConnector(url);
                return connector.GetAssetPermissions(userID, assetID, sessionID);
            }
            return 0;
        }
        
        private OSDMap convertInventoryItemToOSD(InventoryItemBase invItem)
        {
            OSDMap requestMap = new OSDMap();
            
            requestMap["Creator"] = OSD.FromString(invItem.CreatorId);
            requestMap["Owner"] = OSD.FromString(invItem.Owner.ToString());
            requestMap["GroupID"] = OSD.FromString(invItem.GroupID.ToString());
            requestMap["ID"] = OSD.FromString(invItem.ID.ToString());
            requestMap["AssetID"] = OSD.FromString(invItem.AssetID.ToString());
            requestMap["AssetType"] = OSD.FromInteger(invItem.AssetType);
            requestMap["Folder"] = OSD.FromString(invItem.Folder.ToString());
            requestMap["Name"] = OSD.FromString(invItem.Name);
            requestMap["Description"] = OSD.FromString(invItem.Description);
            requestMap["NextPermissions"] = OSD.FromInteger((uint)invItem.NextPermissions);
            requestMap["CurrentPermissions"] = OSD.FromInteger((uint)invItem.CurrentPermissions);
            requestMap["BasePermissions"] = OSD.FromInteger((uint)invItem.BasePermissions);
            requestMap["EveryOnePermissions"] = OSD.FromInteger((uint)invItem.EveryOnePermissions);
            requestMap["GroupPermissions"] = OSD.FromInteger((uint)invItem.GroupPermissions);
            requestMap["InvType"] = OSD.FromInteger((int)invItem.InvType);
            requestMap["SalePrice"] = OSD.FromInteger((int)invItem.SalePrice);
            requestMap["SaleType"] = OSD.FromInteger((uint)invItem.SaleType);
            requestMap["CreationDate"] = OSD.FromInteger((int)invItem.CreationDate);
            requestMap["GroupOwned"] = OSD.FromInteger(0);
            requestMap["Flags"] = OSD.FromInteger((uint)invItem.Flags);
            
            
            
            return requestMap;
            
        }
        
        private OSDMap convertInventoryFolderToOSD(InventoryFolderBase folder)
        {
            OSDMap requestMap = new OSDMap();
            
                requestMap["Name"] = OSD.FromString(folder.Name);
                requestMap["ID"] = OSD.FromString(folder.ID.ToString()); 
                requestMap["Owner"] = OSD.FromString(folder.Owner.ToString());
                requestMap["ParentID"] = OSD.FromString(folder.ParentID.ToString()); 
                requestMap["Type"] = OSD.FromInteger((int)folder.Type);
                requestMap["Version"] = OSD.FromInteger((int)folder.Version);   
                
            return requestMap;
        }
        
          private InventoryItemBase OSDToInventoryItemBase(OSDMap requestMap)
        {
            InventoryItemBase resolvedItem = new InventoryItemBase();
//          resolvedItem.Creator =   requestMap["Creator"].AsUUID(); // DWL HACK
            resolvedItem.Owner =   requestMap["Owner"].AsUUID();
            resolvedItem.GroupID = requestMap["GroupID"].AsUUID();
            resolvedItem.ID = requestMap["ID"].AsUUID();
            resolvedItem.AssetID =   requestMap["AssetID"].AsUUID();
            resolvedItem.AssetType= requestMap["AssetType"].AsInteger();
            resolvedItem.Folder =   requestMap["Folder"].AsUUID();
            resolvedItem.Name = requestMap["Name"].AsString();
            resolvedItem.Description= requestMap["Description"].AsString();
            resolvedItem.NextPermissions = (uint) requestMap["NextPermissions"].AsInteger();
            resolvedItem.BasePermissions = (uint) requestMap["BasePermissions"].AsInteger();
            resolvedItem.CurrentPermissions = (uint) requestMap["CurrentPermissions"].AsInteger();
            resolvedItem.EveryOnePermissions = (uint) requestMap["EveryOnePermissions"].AsInteger();
            resolvedItem.GroupPermissions = (uint) requestMap["GroupPermissions"].AsInteger();
            resolvedItem.InvType = requestMap["InvType"].AsInteger();
            resolvedItem.SalePrice = requestMap["SalePrice"].AsInteger();
            resolvedItem.SaleType = (byte) requestMap["SaleType"].AsInteger();
            resolvedItem.CreationDate = requestMap["CreationDate"].AsInteger();
            resolvedItem.GroupOwned = requestMap["GroupOwned"].AsBoolean();
            resolvedItem.Flags =  (uint)requestMap["Flags"].AsInteger();
            
            return resolvedItem;
        }
        
        private InventoryFolderBase OSDToInventoryFolderBase(OSDMap requestMap)
        {
            InventoryFolderBase folder = new InventoryFolderBase();
            folder.Name = requestMap["Name"].AsString();
            folder.ID = requestMap["ID"].AsUUID();
            folder.Owner = requestMap["Owner"].AsUUID();
            folder.ParentID = requestMap["ParentID"].AsUUID();
            folder.Type = (short)requestMap["Type"].AsInteger();
            folder.Version = (ushort)requestMap["Version"].AsInteger();
                        
            
            return folder;
        }
        
        // forward events onto the users Agent Service.
        private OSDMap SendRequest(OSDMap requestMap, string capAddress)
        {
            m_log.Debug("[ASInventory]: SendRequest " + capAddress);
             
           
            WebRequest createRequest = null; 
            try 
            { 
                createRequest = WebRequest.Create(capAddress);
                createRequest.Method = "POST"; 
                createRequest.ContentType = "application/xml+llsd";  
            } 
            catch (Exception ex) 
            { 
                m_log.InfoFormat("[ASInventory]: Bad URL inventory create cap {0} {1}", ex.Message,capAddress); 
                
            } 
            
            byte[] buffer1 = OSDParser.SerializeLLSDXmlBytes(requestMap);
             
             // Try to post the rez avatar reqest 
            Stream os1 = null; 
            try 
            { // send the Post 
                createRequest.ContentLength = buffer1.Length; //Count bytes to send 
                os1 = createRequest.GetRequestStream(); 
                os1.Write(buffer1, 0, buffer1.Length); //Send it 
                os1.Close(); 
                m_log.InfoFormat("[ASInventory]: Sent inventory request to cap {0}", capAddress); 
            } 
            catch (Exception ex) 
            { 
                m_log.InfoFormat("[ASInventory]: Bad send on derez_avatar {0}", ex.Message); 
            } 
        
            // Now we get to read the response 
            m_log.Info("[ASInventory]: waiting for a reply after inventory request send"); 
            
             // Read the reply 
            OSD resolverResponse = null; 
            OSDMap invResponseMap = new OSDMap(); 
            string invResponse;
            
            try 
                { 
                    WebResponse webResponse = createRequest.GetResponse(); 
                    if (webResponse == null) 
                    { 
                        m_log.Info("[ASInventory]: Null reply on Inventory create post"); 
                        return null;
                    } 
                    StreamReader sr = new StreamReader(webResponse.GetResponseStream()); 
                    invResponse = sr.ReadToEnd().Trim(); 
                    m_log.InfoFormat("[ASInventory]: InventoryReflector reply was {0} ", invResponse); 
                    resolverResponse = OSDParser.Deserialize(invResponse);
                    invResponseMap = (OSDMap) resolverResponse;
                    return invResponseMap;
                } 
                catch (WebException ex) 
                { 
                    m_log.InfoFormat("[ASInventory]: exception on read after send of inventory create {0}", ex.Message); 
                
                } 
            
            return null;
             
            
        }
    }
}