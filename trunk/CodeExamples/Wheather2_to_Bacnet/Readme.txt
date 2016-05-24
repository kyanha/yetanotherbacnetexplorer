/********************************************************************
*                           MIT License
* 
* Copyright (C) 2016 Frederic Chaxel <fchaxel@free.fr>
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

This program (windows service or console) gets current wheather data
from MyWeather2 Internet Webservice and 'sends' them on Bacnet.

You must first creates a user account into http://www.myweather2.com/
in order to get a UserAccessKey : see Developper Zone for a 2 Day Forecast 
Weather API access.

You have to modify and add information into the registry : have a look to 
the .Reg file (UserAccessKey, Latitude, Longitude must be modified).

On a Win32 only PC, removes Wow6432Node from the .reg file entry :
	[HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Wheather2_to_Bacnet]
	changes by
	[HKEY_LOCAL_MACHINE\SOFTWARE\Wheather2_to_Bacnet]

This .reg file should be registered by a double click.

To use it as a console application just run it with any kind of parameters 
such as 'Wheather2_to_Bacnet Console' 

A parameter is already set into the Visual Studio project user option file, 
so debug mode will be OK as a console application.

To register the Windows service : in an admin console type :
	installutil.exe Wheather2_to_Bacnet.exe
installutil is located on Windows\Microsoft.NET\Framework\V4xxxx

then start manually the service or reboot the pc


The code could be simply ported to Linux-Mono :
	removes the 3 lines of code concerning services in the Main method
	hard code (for instance) the 3 parameters : UserAccessKey ...
	removes the inheritence between MyService and ServiceBase
	removes MyServiceInstaller class