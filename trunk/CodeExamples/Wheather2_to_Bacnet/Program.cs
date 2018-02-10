﻿/**************************************************************************
*                           MIT License
* 
* Copyright (C) 2016 Frederic Chaxel <fchaxel@free.fr>
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
using System.Net;
using System.IO;
using System.Xml;
using System.Threading;
using System.ServiceProcess;
using System.Configuration.Install;
using Microsoft.Win32;
using BaCSharp;
using AnotherStorageImplementation;
using System.IO.BACnet;
using System.Globalization;

namespace Weather2_to_Bacnet
{
    public class MyService : System.ServiceProcess.ServiceBase
    {
        const int UpdateDelay = 10; // Requests are sent to myweather2 every 10 minutes
                                    // This ~1/3 the maximum for the free service

        ManualResetEvent StopSrv = new ManualResetEvent(false);

        // BacnetObjects dictionnary
        DeviceObject device;
        AnalogInput<float> Temp, Windspeed, Humidity, Pressure, DewPoint, VaporPressure;
        TrendLog TrendTemp;
        CharacterString Windsdir, WeatherDescr;
        BacnetDateTime SunSet, SunRise, Updatetime, NextUpdatetime;

        // An alternative is to embbed data into code, or to use another way (configuration file, ...)
        string BacnetDeviceId = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Weather2_to_Bacnet", "BacnetDeviceId", null);
        string AppId = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Weather2_to_Bacnet", "AppId", null);
        string UserAccessKey = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Weather2_to_Bacnet", "UserAccessKey", null);
        string Latitude = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Weather2_to_Bacnet", "Latitude", null);
        string Longitude = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Weather2_to_Bacnet", "Longitude", null);
        string Lang = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Weather2_to_Bacnet", "Lang", null);

        public bool RunAsConsoleApp()
        {
            new Thread(WorkingLoop).Start();

            if ((UserAccessKey == null) || (Latitude == null) || (Longitude == null))
                return false;
            else
                return true;
        }


        private string Wheather2_Request(string AppId, string UserAccessKey, string Lat, string Long, string Lg="fr")
        {
            try
            {

                string Url = "http://api.weatherunlocked.com/api/current/" + Lat + "," + Long + "?app_id=" + AppId + "&app_key=" + UserAccessKey+"&lang="+Lg+"";

                HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(Url);
                req.Accept = "text/xml";
                WebResponse resp = req.GetResponse();
                
                StreamReader respReader = new StreamReader(resp.GetResponseStream());
                String response = respReader.ReadToEnd();

                resp.Close();

                return response;
            }
            catch
            {
                return null;
            }
        }

        private void SetAIValue(AnalogInput<float> AI, XmlNode NodeVal)
        {
            try
            {
                AI.internal_PROP_PRESENT_VALUE = Convert.ToSingle(NodeVal.InnerText, CultureInfo.InvariantCulture);
                AI.m_PROP_OUT_OF_SERVICE = false;
                AI.m_PROP_STATUS_FLAGS.SetBit((byte)3, false);
            }
            catch
            {
                AI.m_PROP_OUT_OF_SERVICE = true;
                AI.m_PROP_STATUS_FLAGS.SetBit((byte)3, true);
            }

        }

        private void ParseWheather2_Response(String Rep)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(Rep);

                XmlNode node = doc.SelectSingleNode("/CurrentWeather/temperature_c");
                SetAIValue(Temp, node);
                if (Temp.m_PROP_OUT_OF_SERVICE == false)
                    TrendTemp.AddValue(Temp.internal_PROP_PRESENT_VALUE, 0);

                node = doc.SelectSingleNode("/CurrentWeather/wind_speed_kmh");
                SetAIValue(Windspeed, node);

                node = doc.SelectSingleNode("/CurrentWeather/wind_direction_com");
                Windsdir.internal_PROP_PRESENT_VALUE = node.InnerText;

                node = doc.SelectSingleNode("/CurrentWeather/humidity");
                SetAIValue(Humidity, node);

                node = doc.SelectSingleNode("/CurrentWeather/air_pressure_mb");
                SetAIValue(Pressure, node);

                node = doc.SelectSingleNode("/CurrentWeather/weather_description");
                WeatherDescr.internal_PROP_PRESENT_VALUE = node.InnerText;

                // http://www.meteo.psu.edu/~jyh10/classes/meteo473/java-tdew.htm
                // by Jerry Y. Harrington
                if ((Humidity.m_PROP_OUT_OF_SERVICE == false) || (Temp.m_PROP_OUT_OF_SERVICE == false))
                {
                    double es = 6.112 * Math.Exp(1.0 * 17.67 * Temp.internal_PROP_PRESENT_VALUE / (243.5 + Temp.internal_PROP_PRESENT_VALUE));
                    double ed = Humidity.internal_PROP_PRESENT_VALUE / 100.0 * es;
                    double eln = Math.Log(ed / 6.112);
                    double td = -243.5 * eln / (eln - 17.67);

                    DewPoint.internal_PROP_PRESENT_VALUE = (float)Math.Round(td, 1);
                    DewPoint.m_PROP_OUT_OF_SERVICE = false;
                    DewPoint.m_PROP_STATUS_FLAGS.SetBit((byte)3, false);
                    VaporPressure.internal_PROP_PRESENT_VALUE = (float)Math.Round(es, 1);
                    VaporPressure.m_PROP_OUT_OF_SERVICE = false;
                    VaporPressure.m_PROP_STATUS_FLAGS.SetBit((byte)3, false);
                }
                else
                {
                    DewPoint.m_PROP_OUT_OF_SERVICE = true; 
                    DewPoint.m_PROP_STATUS_FLAGS.SetBit((byte)3, true);
                    VaporPressure.m_PROP_OUT_OF_SERVICE = true;
                    VaporPressure.m_PROP_STATUS_FLAGS.SetBit((byte)3, true);
                }

                Updatetime.m_PresentValue = DateTime.Now;

            }
            catch { }
        }

        void InitBacnetDictionary()
        {
            uint deviceId;

            if (UInt32.TryParse(BacnetDeviceId, out deviceId) == false)
                deviceId = 12345; // default value

            device = new DeviceObject(deviceId, "Weather2 to Bacnet ", "Weather2 data", false);

            if ((UserAccessKey != null) && (Latitude != null) && (Longitude != null))
            {
                Temp = new AnalogInput<float>
                (
                    0,
                    "Temperature",
                    "Temperature",
                    0,
                    BacnetUnitsId.UNITS_DEGREES_CELSIUS
                );

                // 24h trendlog
                TrendTemp = new TrendLog(0, "Temperature Trend", "Temperature Trend", 6 * 24, BacnetTrendLogValueType.TL_TYPE_SIGN);

                Windspeed = new AnalogInput<float>
                (
                    1,
                    "Windspeed",
                    "Wind speed",
                    0,
                    BacnetUnitsId.UNITS_KILOMETERS_PER_HOUR
                );
                Humidity = new AnalogInput<float>
                (
                    2,
                    "Humidity",
                    "Humidity",
                    0,
                    BacnetUnitsId.UNITS_PERCENT
                );
                Pressure = new AnalogInput<float>
                (
                    3,
                    "Pressure",
                    "Pressure",
                    0,
                    BacnetUnitsId.UNITS_HECTOPASCALS
                );

                DewPoint = new AnalogInput<float>
                (
                    4,
                    "DewPoint",
                    "Dew Point",
                    0,
                    BacnetUnitsId.UNITS_DEGREES_CELSIUS
                );

                VaporPressure = new AnalogInput<float>
                (
                    5,
                    "VaporPressure",
                    "Equilibrium Vapor Pressure",
                    0,
                    BacnetUnitsId.UNITS_HECTOPASCALS
                );

                Windsdir = new CharacterString
                (0, "Winddir", "Wind Direction", "Not available", false);

                WeatherDescr = new CharacterString
                (1, "WeatherDescr", "Weather Description", "Not available", false);

                SunRise = new BacnetDateTime(0, "Sunrise", "Sun up time");
                SunSet = new BacnetDateTime(1, "Sunset", "Sun down time");
                Updatetime = new BacnetDateTime(2, "Updatetime", "Date & Time of the current values");
                NextUpdatetime = new BacnetDateTime(3, "NextUpdatetime", "Date & Time of the next request");

                device.AddBacnetObject(Temp);
                device.AddBacnetObject(TrendTemp);
                device.AddBacnetObject(Windspeed);
                device.AddBacnetObject(Humidity);
                device.AddBacnetObject(Pressure);
                device.AddBacnetObject(DewPoint);
                device.AddBacnetObject(VaporPressure);
                device.AddBacnetObject(Windsdir);
                device.AddBacnetObject(WeatherDescr);
                device.AddBacnetObject(SunRise);
                device.AddBacnetObject(SunSet);
                device.AddBacnetObject(Updatetime);
                device.AddBacnetObject(NextUpdatetime);
                device.AddBacnetObject(new NotificationClass(0, "Notification", "Notification"));
            }
            else
                device.m_PROP_SYSTEM_STATUS = BacnetDeviceStatus.NON_OPERATIONAL;

            // Force the JIT compiler to make some job before network access
            device.Cli2Native();
            BacnetActivity.StartActivity(device);
        }

        void WorkingLoop()
        {

            InitBacnetDictionary();

            if ((UserAccessKey == null) || (Latitude == null) || (Longitude == null))
            {
                StopSrv.WaitOne();
                return;
            }

            double lat=0, lon=0;
            try
            {
                lat = Convert.ToDouble(Latitude, CultureInfo.InvariantCulture);
                lon = Convert.ToDouble(Longitude, CultureInfo.InvariantCulture);
            }
            catch { }

            DateTime today = DateTime.MinValue;

            for (; ; )
            {
                if (DateTime.Today != today) // sunset & sunride time, computed once a day
                {
                    today = DateTime.Today;

                    double JD = NAA.Util.calcJD(today);
                    double sunRise = NAA.Util.calcSunRiseUTC(JD, lat, lon);

                    double sunSet = NAA.Util.calcSunSetUTC(JD, lat, lon);

                    var v = NAA.Util.getDateTime(sunRise, 0, today, false);

                    DateTime? dSunRise = NAA.Util.getDateTime(sunRise, 0, today, false);
                    DateTime? dSunSet = NAA.Util.getDateTime(sunSet, 0, today, false);

                    if (dSunRise!=null)
                        SunRise.m_PresentValue = dSunRise.Value.ToLocalTime();
                    if (dSunSet != null)
                        SunSet.m_PresentValue = dSunSet.Value.ToLocalTime();
                   
                }

                // Read wheather data from the webservice
                String xmlRep = Wheather2_Request(AppId, UserAccessKey, Latitude, Longitude, Lang);
                if (xmlRep != null)
                    ParseWheather2_Response(xmlRep);

                NextUpdatetime.m_PresentValue = DateTime.Now.AddMinutes(UpdateDelay);

                // Wait 10 minutes or a stop condition
                if (StopSrv.WaitOne(new TimeSpan(0, UpdateDelay, 0)) == true)
                    return;
            }
        }

        Thread m_thread;
        protected override void OnStart(string[] args)
        {
            m_thread = new Thread(WorkingLoop);
            m_thread.Start();
            base.OnStart(args);
        }

        protected override void OnStop()
        {
            StopSrv.Set();
            // wait for the thread to stop giving it 20 seconds
            m_thread.Join(20000);

            base.OnStop();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // detect Console Mode
            if (Environment.UserInteractive)
            {
                Console.WriteLine("Running as a console application");
                Console.WriteLine("Can be installed and started as a Windows services");
                Console.WriteLine("... see the associated Readme file");
                MyService ConsoleModeApp = new MyService();
                if (ConsoleModeApp.RunAsConsoleApp() == false)
                {
                    Console.WriteLine("\nError : Unable to find Parameters in the windows registry");
                    Console.WriteLine("\tsee the associated Readme file and");
                    Console.WriteLine("\tWheather2config.reg in "+Directory.GetCurrentDirectory());
                }
            }
            else
            {
                ServiceBase[] servicesToRun;
                servicesToRun = new ServiceBase[] { new MyService() };
                ServiceBase.Run(servicesToRun);
            }
        }
    }

    [System.ComponentModel.RunInstaller(true)]
    public class MyServiceInstaller : System.Configuration.Install.Installer
    {
        public MyServiceInstaller()
        {
            ServiceProcessInstaller process = new ServiceProcessInstaller();

            process.Account = ServiceAccount.LocalSystem;

            ServiceInstaller serviceAdmin = new ServiceInstaller();

            serviceAdmin.StartType = ServiceStartMode.Automatic;
            serviceAdmin.ServiceName = "WeatherBacnet";
            serviceAdmin.DisplayName = "Weather2 to Bacnet";
            serviceAdmin.Description = "Bridge myWeather2 to Bacnet";

            Installers.Add(process);
            Installers.Add(serviceAdmin);
        }
    }
}
