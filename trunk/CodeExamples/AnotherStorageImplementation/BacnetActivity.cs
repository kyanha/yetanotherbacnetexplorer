﻿/**************************************************************************
*                           MIT License
* 
* Copyright (C) 2014 Morten Kvistgaard <mk@pch-engineering.dk>
* Copyright (C) 2015 Frederic Chaxel <fchaxel@free.fr>
*
* Permission is hereby granted, free of charge, to any person obtaining
* a copy of this software and associated documentation files (the
* "Software"), to deal in the Software without restriction, including
* without limitation the rights to use, copy, modify, merge, publish,
* distribute, sublicense, and/or sell copies of the Software, and to
* permit persons to whom the Software is furnished to do so, subject to
* the following conditions:
*
* The above copyright notice and this permission notice shall be included
* in all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
* EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
* MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
* IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
* CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
* TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
* SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*
*********************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.BACnet;
using System.Reflection;
using BaCSharp;

namespace AnotherStorageImplementation
{
    static class BacnetActivity
    {
        static BacnetClient bacnet_client;
        static uint deviceId;
        static DeviceObject device;

        public static void StartActivity(DeviceObject _device)
        {
            deviceId=_device.PROP_OBJECT_IDENTIFIER.instance;
            device=_device;

            // Bacnet on UDP/IP/Ethernet
            bacnet_client = new BacnetClient(new BacnetIpUdpProtocolTransport(0xBAC0, false));

            bacnet_client.OnIam += new BacnetClient.IamHandler(handler_OnIam);
            bacnet_client.OnReadPropertyRequest += new BacnetClient.ReadPropertyRequestHandler(handler_OnReadPropertyRequest);
            bacnet_client.OnReadPropertyMultipleRequest += new BacnetClient.ReadPropertyMultipleRequestHandler(handler_OnReadPropertyMultipleRequest);
            bacnet_client.OnWritePropertyRequest += new BacnetClient.WritePropertyRequestHandler(handler_OnWritePropertyRequest);
            bacnet_client.OnSubscribeCOV += new BacnetClient.SubscribeCOVRequestHandler(handler_OnSubscribeCOV);
            bacnet_client.OnSubscribeCOVProperty += new BacnetClient.SubscribeCOVPropertyRequestHandler(handler_OnSubscribeCOVProperty);
            bacnet_client.OnReadRange += new BacnetClient.ReadRangeHandler(handler_OnReadRange);
            bacnet_client.OnAtomicWriteFileRequest += new BacnetClient.AtomicWriteFileRequestHandler(handler_OnAtomicWriteFileRequest);
            bacnet_client.OnAtomicReadFileRequest += new BacnetClient.AtomicReadFileRequestHandler(handler_OnAtomicReadFileRequest);

            BaCSharpObject.OnInternalCOVNotify += new BaCSharpObject.WriteNotificationCallbackHandler(handler_OnCOVManagementNotify);

            bacnet_client.Start();    // go
            // Send Iam
            bacnet_client.Iam(deviceId, new BacnetSegmentations());

            if (_device.FindBacnetObjectType(BacnetObjectTypes.OBJECT_NOTIFICATION_CLASS))
            {
                bacnet_client.OnWhoIs += new BacnetClient.WhoIsHandler(handler_OnWhoIs);
                bacnet_client.WhoIs();                          // Send WhoIs : needed BY Notification class for deviceId<->IP endpoint
                device.SetIpEndpoint(bacnet_client); // Register the endpoint for IP Notification usage
            }
        }

        static void handler_OnIam(BacnetClient sender, BacnetAddress adr, uint device_id, uint max_apdu, BacnetSegmentations segmentation, ushort vendor_id)
        {
            device.ReceivedIam(sender, adr, device_id);
        }

        private static void handler_OnAtomicReadFileRequest(BacnetClient sender, BacnetAddress adr, byte invoke_id, bool is_stream, BacnetObjectId object_id, int position, uint count, BacnetMaxSegments max_segments)
        {
            lock (device)
            {

                BaCSharpObject File = device.FindBacnetObject(object_id);
                if (File is BacnetFile)
                {
                    try
                    {
                        BacnetFile f = (BacnetFile)File;

                        int filesize = (int)f.PROP_FILE_SIZE;
                        bool end_of_file = (position + count) >= filesize;
                        count = (uint)Math.Min(count, filesize - position);
                        int max_filebuffer_size = sender.GetFileBufferMaxSize();
                        if (count > max_filebuffer_size && max_segments > 0)
                        {
                            //create segmented message!!!
                        }
                        else
                        {
                            count = (uint)Math.Min(count, max_filebuffer_size);     //trim
                        }

                        byte[] file_buffer = f.ReadFileBlock(position, (int)count);                       
                        sender.ReadFileResponse(adr, invoke_id, sender.GetSegmentBuffer(max_segments), position, count, end_of_file, file_buffer);
                    }
                    catch (Exception)
                    {
                        sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_ATOMIC_READ_FILE, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
                    }
                }
                else
                    sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_ATOMIC_READ_FILE, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
            }
        }

        // Here something could be done to avoid a to big fill to be written on the disk
        private static void handler_OnAtomicWriteFileRequest(BacnetClient sender, BacnetAddress adr, byte invoke_id, bool is_stream, BacnetObjectId object_id, int position, uint block_count, byte[][] blocks, int[] counts, BacnetMaxSegments max_segments)
        {
            lock (device)
            {
                BaCSharpObject File = device.FindBacnetObject(object_id);
                if (File is BacnetFile)
                {
                    try
                    {
                        BacnetFile f = (BacnetFile)File;

                        if (f.PROP_READ_ONLY == false)
                        {
                            int currentposition = position;
                            for (int i = 0; i < block_count; i++)
                            {
                                f.WriteFileBlock(blocks[i], currentposition, counts[i]);
                                currentposition += counts[i];
                            }
                            sender.WriteFileResponse(adr, invoke_id, sender.GetSegmentBuffer(max_segments), position);
                        }
                        else
                            sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_ATOMIC_WRITE_FILE, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_WRITE_ACCESS_DENIED);
                    }
                    catch (Exception)
                    {
                        sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_ATOMIC_WRITE_FILE, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
                    }
                }
                else
                    sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_ATOMIC_WRITE_FILE, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
            }
        }

        /*****************************************************************************************************/
        static void handler_OnReadRange(BacnetClient sender, BacnetAddress adr, byte invoke_id, BacnetObjectId objectId, BacnetPropertyReference property, System.IO.BACnet.Serialize.BacnetReadRangeRequestTypes requestType, uint position, DateTime time, int count, BacnetMaxSegments max_segments)
        {
            lock (device)
            {
                BaCSharpObject trend = device.FindBacnetObject(objectId);

                if (trend is TrendLog)
                {
                    BacnetResultFlags status;
                    byte[] application_data = (trend as TrendLog).GetEncodedTrends(position, count, out status);

                    if (application_data != null)
                    {
                        //send
                        sender.ReadRangeResponse(adr, invoke_id, sender.GetSegmentBuffer(max_segments), objectId, property, status, (uint)count, application_data, requestType, position);
                    }
                }
                else
                    sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_RANGE, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);    
           }
        }
        /*****************************************************************************************************/
        static void handler_OnSubscribeCOV(BacnetClient sender, BacnetAddress adr, byte invoke_id, uint subscriberProcessIdentifier, BacnetObjectId monitoredObjectIdentifier, bool cancellationRequest, bool issueConfirmedNotifications, uint lifetime, BacnetMaxSegments max_segments)
        {
            lock (device)
            {
                BaCSharpObject bacobj = device.FindBacnetObject(monitoredObjectIdentifier);
                if (bacobj != null)
                {
                    //create 
                    Subscription sub = SubscriptionManager.HandleSubscriptionRequest(sender, adr, invoke_id, subscriberProcessIdentifier, monitoredObjectIdentifier, (uint)BacnetPropertyIds.PROP_ALL, cancellationRequest, issueConfirmedNotifications, lifetime, 0);

                    //send confirm
                    sender.SimpleAckResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_SUBSCRIBE_COV, invoke_id);

                    //also send first values
                    if (!cancellationRequest)
                    {
                        System.Threading.ThreadPool.QueueUserWorkItem((o) =>
                        {
                            IList<BacnetPropertyValue> values;
                            if (bacobj.ReadPropertyAll(out values))
                                sender.Notify(adr, sub.subscriberProcessIdentifier, deviceId, sub.monitoredObjectIdentifier, (uint)sub.GetTimeRemaining(), sub.issueConfirmedNotifications, values);

                        }, null);
                    }
                }
                else
                    sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_SUBSCRIBE_COV, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
            }
        }
        /*****************************************************************************************************/
        static void handler_OnSubscribeCOVProperty(BacnetClient sender, BacnetAddress adr, byte invoke_id, uint subscriberProcessIdentifier, BacnetObjectId monitoredObjectIdentifier, BacnetPropertyReference monitoredProperty, bool cancellationRequest, bool issueConfirmedNotifications, uint lifetime, float covIncrement, BacnetMaxSegments max_segments)
        {
            lock (device)
            {
                BaCSharpObject bacobj = device.FindBacnetObject(monitoredObjectIdentifier);
                if (bacobj != null)
                {
                    //create 
                    Subscription sub = SubscriptionManager.HandleSubscriptionRequest(sender, adr, invoke_id, subscriberProcessIdentifier, monitoredObjectIdentifier, monitoredProperty.propertyIdentifier, cancellationRequest, issueConfirmedNotifications, lifetime, covIncrement);

                    //send confirm
                    sender.SimpleAckResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_SUBSCRIBE_COV_PROPERTY, invoke_id);
                    
                    //also send first values
                    if (!cancellationRequest)
                    {
                        System.Threading.ThreadPool.QueueUserWorkItem((o) =>
                        {
                            IList<BacnetValue> _values;
                            bacobj.ReadPropertyValue(monitoredProperty, out _values);

                            List<BacnetPropertyValue> values = new List<BacnetPropertyValue>();
                            BacnetPropertyValue tmp = new BacnetPropertyValue();
                            tmp.property = sub.monitoredProperty;
                            tmp.value = _values;
                            values.Add(tmp);

                            sender.Notify(adr, sub.subscriberProcessIdentifier, deviceId, sub.monitoredObjectIdentifier, (uint)sub.GetTimeRemaining(), sub.issueConfirmedNotifications, values);
                        }, null);
                    }
                }
                else
                    sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_SUBSCRIBE_COV_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
            } 
        }
        /*****************************************************************************************************/
        static void handler_OnCOVManagementNotify(BaCSharpObject sender, BacnetPropertyIds propId)
        {
            System.Threading.ThreadPool.QueueUserWorkItem((o) =>
            {
                lock (device)
                {
                    //remove old leftovers
                    SubscriptionManager.RemoveOldSubscriptions();

                    //find subscription
                    List<Subscription> subs = SubscriptionManager.GetSubscriptionsForObject(sender.PROP_OBJECT_IDENTIFIER);

                    if (subs==null) return; // nobody

                    //Read the property
                    IList<BacnetValue> value;
                    BacnetPropertyReference br = new BacnetPropertyReference((uint)propId, (uint)System.IO.BACnet.Serialize.ASN1.BACNET_ARRAY_ALL);
                    ErrorCodes error = sender.ReadPropertyValue(br, out value);

                    List<BacnetPropertyValue> values = new List<BacnetPropertyValue>();
                    BacnetPropertyValue tmp = new BacnetPropertyValue();
                    tmp.value = value;
                    tmp.property = br;
                    values.Add(tmp);

                    //send to all
                    foreach (Subscription sub in subs)
                    {
                        if (sub.monitoredProperty.propertyIdentifier == (uint)BacnetPropertyIds.PROP_ALL || sub.monitoredProperty.propertyIdentifier == (uint)propId)
                        {
                            tmp.property = sub.monitoredProperty;
                            if (!sub.reciever.Notify(sub.reciever_address, sub.subscriberProcessIdentifier, deviceId, sub.monitoredObjectIdentifier, (uint)sub.GetTimeRemaining(), sub.issueConfirmedNotifications, values))
                                SubscriptionManager.RemoveReceiver(sub.reciever_address);
                        }
                    }
                }
            }, null);
        }
        /*****************************************************************************************************/
        static void handler_OnWritePropertyRequest(BacnetClient sender, BacnetAddress adr, byte invoke_id, BacnetObjectId object_id, BacnetPropertyValue value, BacnetMaxSegments max_segments)
        {
            lock (device)
            {
                BaCSharpObject bacobj = device.FindBacnetObject(object_id);
                if (bacobj != null)
                {
                    ErrorCodes error = bacobj.WritePropertyValue(value, true);
                    if (error == ErrorCodes.Good)
                        sender.SimpleAckResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY, invoke_id);
                    else
                    {
                        BacnetErrorCodes bacEr=BacnetErrorCodes.ERROR_CODE_OTHER;
                        if (error == ErrorCodes.WriteAccessDenied)
                            bacEr = BacnetErrorCodes.ERROR_CODE_WRITE_ACCESS_DENIED;
                        if (error == ErrorCodes.OutOfRange)
                            bacEr = BacnetErrorCodes.ERROR_CODE_VALUE_OUT_OF_RANGE;

                        sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, bacEr);
                    }
                }
                else
                    sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_UNKNOWN_OBJECT);      
            }
        }

        /*****************************************************************************************************/
        static void handler_OnReadPropertyRequest(BacnetClient sender, BacnetAddress adr, byte invoke_id, BacnetObjectId object_id, BacnetPropertyReference property, BacnetMaxSegments max_segments)
        {
            lock (device)
            {
                BaCSharpObject bacobj=device.FindBacnetObject(object_id);

                if (bacobj != null)
                {
                    IList<BacnetValue> value;
                    ErrorCodes error= bacobj.ReadPropertyValue(property, out value);
                    if (error == ErrorCodes.Good)
                        sender.ReadPropertyResponse(adr, invoke_id, sender.GetSegmentBuffer(max_segments), object_id, property, value);
                    else
                        sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_UNKNOWN_PROPERTY);
                }
                else
                    sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_UNKNOWN_OBJECT);
            }
        }

        /*****************************************************************************************************/
        static void handler_OnReadPropertyMultipleRequest(BacnetClient sender, BacnetAddress adr, byte invoke_id, IList<BacnetReadAccessSpecification> properties, BacnetMaxSegments max_segments)
        {
            lock (device)
            {
                try
                {
                    IList<BacnetPropertyValue> value;
                    List<BacnetReadAccessResult> values = new List<BacnetReadAccessResult>();
                    foreach (BacnetReadAccessSpecification p in properties)
                    {
                        if (p.propertyReferences.Count == 1 && p.propertyReferences[0].propertyIdentifier == (uint)BacnetPropertyIds.PROP_ALL)
                        {                            
                            BaCSharpObject bacobj=device.FindBacnetObject(p.objectIdentifier);
                            if (!bacobj.ReadPropertyAll(out value))
                            {
                                sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROP_MULTIPLE, invoke_id, BacnetErrorClasses.ERROR_CLASS_OBJECT, BacnetErrorCodes.ERROR_CODE_UNKNOWN_OBJECT);
                                return;
                            }
                        }
                        else
                        {
                            BaCSharpObject bacobj=device.FindBacnetObject(p.objectIdentifier);
                            bacobj.ReadPropertyMultiple(p.propertyReferences, out value);
                        }
                        values.Add(new BacnetReadAccessResult(p.objectIdentifier, value));
                    }
                    sender.ReadPropertyMultipleResponse(adr, invoke_id, sender.GetSegmentBuffer(max_segments), values);
                }
                catch (Exception)
                {
                    sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROP_MULTIPLE, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
                }
            }
        }

        /*****************************************************************************************************/
        static void handler_OnWhoIs(BacnetClient sender, BacnetAddress adr, int low_limit, int high_limit)
        {
            if (low_limit != -1 && deviceId < low_limit) return;
            else if (high_limit != -1 && deviceId > high_limit) return;
            sender.Iam(deviceId, new BacnetSegmentations());
        }
    }
}
