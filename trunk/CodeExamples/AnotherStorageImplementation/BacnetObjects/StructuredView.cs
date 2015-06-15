﻿/**************************************************************************
*                           MIT License
* 
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

namespace BaCSharp
{
    // Not Serializable since deviceobject is not serializable
    public class StructuredView : BaCSharpObject
    {
        public List<BacnetValue> m_PROP_SUBORDINATE_LIST = new List<BacnetValue>();
        [BaCSharpType(BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID)]
        public virtual List<BacnetValue> PROP_SUBORDINATE_LIST
        {
            get { return m_PROP_SUBORDINATE_LIST; }
        }

        public StructuredView(int ObjId, String ObjName, String Description)
            : base(new BacnetObjectId(BacnetObjectTypes.OBJECT_STRUCTURED_VIEW, (uint)ObjId), ObjName,  Description)
        {
        }

        public StructuredView() { }

        public void AddBacnetObject(BaCSharpObject newObj)
        {
            m_PROP_SUBORDINATE_LIST.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, newObj.PROP_OBJECT_IDENTIFIER));
            // Each object provided by the server must be added one by one to the DeviceObject
            Mydevice.AddBacnetObject(newObj);
        }
    }
}
