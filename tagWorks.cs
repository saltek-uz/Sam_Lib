using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sam_Lib
{
    internal class tagWorks
    {
        [DllImport("CR95HF.dll")] public static extern UInt32 CR95HFDLL_USBconnect();
        [DllImport("CR95HF.dll")] public static extern UInt32 CR95HFDLL_getHardwareVersion(StringBuilder returnString);
        [DllImport("CR95HF.dll")] public static extern UInt32 CR95HFDLL_getMCUrev(StringBuilder returnString);
        [DllImport("CR95HF.dll")] public static extern UInt32 CR95HFDll_GetDLLrev(StringBuilder returnString);
        [DllImport("CR95HF.dll")] public static extern UInt32 CR95HFDll_Idn(StringBuilder returnString);
        [DllImport("CR95HF.dll")] public static extern UInt32 CR95HFDll_Echo(StringBuilder returnString);
        [DllImport("CR95HF.dll")] public static extern UInt32 CR95HFDll_FieldOff(StringBuilder returnString);

        [DllImport("CR95HF.dll")] public static extern UInt32 CR95HFDll_ResetSPI(StringBuilder returnString);
        [DllImport("CR95HF.dll")] public static extern UInt32 CR95HFDll_SendIRQPulse(StringBuilder returnString);
        [DllImport("CR95HF.dll")] public static extern UInt32 CR95HFDLL_getInterfacePinState(StringBuilder returnString);
        
        [DllImport("CR95HF.dll")] public static extern UInt32 CR95HFDll_STCmd(string sendString, StringBuilder returnString);
        [DllImport("CR95HF.dll")] public static extern UInt32 CR95HFDll_Select(string sendString, StringBuilder returnString);



        //Public Declare Function CR95HFDll_SendReceive Lib "CR95HF.dll" (ByVal mycmdstring As String, ByVal mystring As String) As Long


        //Public Declare Function CR95HFDll_Polling_Reading Lib "CR95HF.dll" (ByVal mystring As String) As Long

        //Public Declare Function CR95HFDll_SendNSSPulse Lib "CR95HF.dll" (ByVal mystring As String) As Long

        //'added in DLL revision 0.6
        //Public Declare Function CR95HFDLL_USBhandlecheck Lib "CR95HF.dll" () As Long


        public class oneTag
        {
            public string tagID { get;set; }
            public byte AFI { get; set; }
            public bool isConnected { get; set; }
        }

        public List<oneTag> tags { get; set;}


        public bool connectReader()
        {

            UInt32 res = CR95HFDLL_USBconnect();
            if (res == 0) return true; else return false;
        }

        public string[] metaData()
        {
            string[] res = new string[3];

            StringBuilder   s1 = new StringBuilder(255); CR95HFDll_GetDLLrev(s1);           res[0] = "DLLversion: " + s1.ToString();
                            s1 = new StringBuilder(255); CR95HFDLL_getMCUrev(s1);           res[1] = "MCUversion: " + s1.ToString();
                            s1 = new StringBuilder(255); CR95HFDLL_getHardwareVersion(s1);  res[2] = "HWversion:  " + s1.ToString();
            return res;
        }

        public string inventory(int afi, string addr)
        {

            StringBuilder s1 = new StringBuilder(255); CR95HFDll_Idn(s1);

            return s1.ToString();
        }

        public string antiCollSearch(int afi, string addr)
        {

            string s = "02A0012600";
            StringBuilder s2 = new StringBuilder(512);


            s = "0000"; CR95HFDll_Select(s, s2); // off
            Thread.Sleep(100);
            s = "010D"; CR95HFDll_Select(s, s2); // on
            Thread.Sleep(100);

            s = "02A0012600";
            s2 = new StringBuilder(512);
            CR95HFDll_STCmd(s, s2);

            return s2.ToString();
        }




    }
}
