using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sam_Lib
{
    internal class cardReader
    {
        private int _retCode;
        private int _hCard;
        private int _hContext;
        private int _protocol;
        public bool ConnActive;
        private string _readername; // change depending on reader
        public byte[] SendBuff = new byte[263];
        public byte[] RecvBuff = new byte[263];
        public int SendLen, RecvLen, Aprotocol;
        public Card.SCARD_READERSTATE RdrState;
        public Card.SCARD_IO_REQUEST PioSendRequest;
        private bool _authenticationRequired;
        private string _uid;
        private string _authenticationKey = "FFFFFFFFFFFF";

        private enum RequestType
        {
            Authentication,
            ReadingWriting
        }


        public bool AuthenticationRequired
        {
            get { return _authenticationRequired; }
            set { _authenticationRequired = value; }
        }
        public List<int> BlockNumbers => GetBlockNumberList();
        private int _selectedBlock = 1;
        public int SelectedBlock
        {
            get { return _selectedBlock; }
            set { _selectedBlock =  value; }
        }

        public string AuthenticationKey
        {
            get { return _authenticationKey; }
            set { _authenticationKey = value; }
        }


        private bool _isConnected;

        public bool IsConnected
        {
            get { return _isConnected; }
            set { _isConnected = value;}
        }



        private string _valueToWrite = "Text to record";
        public string ValueToWrite
        {
            get { return _valueToWrite; }
            set { _valueToWrite = value; }
        }



        public void Initialize()
        {
            EstablishContext(); //define hcontext
            SelectDevice();
     //       IsConnectionActiveAsync();
        }

        private async void IsConnectionActiveAsync()
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        IsConnected = ConnectCard();  //define hcard and Protocol
                        Task.Delay(500);
                    }
                    catch (TaskCanceledException)
                    {
                        MessageBox.Show("Bladyyy");
                    }
                }
            });
        }

        private void Write_Click()
        {
            if (ConnectCard())// establish connection to the card: you've declared this from previous post
            {
                if (AuthenticationKey.Length == 12)
                {
                    SubmitText(ValueToWrite, SelectedBlock); // 5 - is the block we are writing data on the card
                    ValueToWrite = "Text to record";
                    Disconnect();
                }
                else
                {
                    MessageBox.Show("Authentication key must be 12 characters long");
                }
            }
        }


        public string Uid
        {
            get { return _uid ; }
            set { _uid = value; }
        }

        public void Connect_Click()
        {
            if (ConnectCard())
            {
                Uid = GetCardUid();
            }
        }

        public bool ConnectCard(bool retry = false) //define hCard and Protocol
        {
            _retCode = Card.SCardConnect(_hContext, _readername, Card.SCARD_SHARE_SHARED,
                Card.SCARD_PROTOCOL_T0 | Card.SCARD_PROTOCOL_T1, ref _hCard, ref _protocol);

            if (_retCode == Card.SCARD_S_SUCCESS)
                return true;

            if (_retCode < 20 && _retCode > -1)
            {
                //For TimeOut
                Card.SCardReleaseContext(_hContext);
                EstablishContext();
                ConnectCard();
            }
            else if (_retCode == Card.SCARD_E_NO_SMARTCARD && !retry)
            {
                //If device not plugged at the first test
                SelectDevice();
                return ConnectCard(true);
            }
            else
            {
                return false;
            }

            return true;
        }

        public string GetCardUid()//only for mifare 1k cards
        {
            string cardUid;
            var receivedUid = new byte[256];
            Card.SCARD_IO_REQUEST request = new Card.SCARD_IO_REQUEST();
            request.dwProtocol = Card.SCARD_PROTOCOL_T1;
            request.cbPciLength = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Card.SCARD_IO_REQUEST));
            var sendBytes = new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 }; //get UID command for Mifare cards
            var outBytes = receivedUid.Length;
            var status = Card.SCardTransmit(_hCard, ref request, ref sendBytes[0], sendBytes.Length, ref request, ref receivedUid[0], ref outBytes);

            if (status != Card.SCARD_S_SUCCESS)
            {
                cardUid = "Error";
            }
            else
            {
                cardUid = BitConverter.ToString(receivedUid.Take(4).ToArray()).Replace("-", string.Empty).ToLower();
            }

            return cardUid;
        }

        public void SelectDevice()
        {
            List<string> temp = GetListReaders();
            var availableReaders = temp.ToArray();
            RdrState = new Card.SCARD_READERSTATE();
            _readername = availableReaders[0];//selecting first device
            //TODO Add combobox to choose reader
            RdrState.RdrName = _readername;
        }

        public List<string> GetListReaders()
        {
            int readerCount = 0;
            List<string> availableReaderList = new List<string>();

            //Make sure a context has been established before 
            //retrieving the list of smartcard readers.
            _retCode = Card.SCardListReaders(_hContext, null, null, ref readerCount);
            if (_retCode != Card.SCARD_S_SUCCESS)
            {
                MessageBox.Show(Card.GetScardErrMsg(_retCode));
                //connActive = false;
            }

            byte[] readersList = new byte[readerCount];

            //Get the list of reader present again but this time add sReaderGroup, retData as 2rd & 3rd parameter respectively.
            _retCode = Card.SCardListReaders(_hContext, null, readersList, ref readerCount);
            if (_retCode != Card.SCARD_S_SUCCESS)
            {
                MessageBox.Show(Card.GetScardErrMsg(_retCode));
            }

            var rName = string.Empty;
            int index = 0;
            if (readerCount > 0)
            {
                // Convert reader buffer to string
                while (readersList[index] != 0)
                {
                    while (readersList[index] != 0)
                    {
                        rName += (char)readersList[index];
                        index++;
                    }

                    //Add reader name to list
                    availableReaderList.Add(rName);
                    rName = string.Empty;
                    index++;
                }
            }
            return availableReaderList;

        }

        internal void EstablishContext()
        {
            _retCode = Card.SCardEstablishContext(Card.SCARD_SCOPE_SYSTEM, 0, 0, ref _hContext);
            if (_retCode != Card.SCARD_S_SUCCESS)
            {
                MessageBox.Show("Check your device and please restart again", "Reader not connected");
                ConnActive = false;
            }
        }

        public void SubmitText(string tmpStr, int block)
        {
            var dangerousBlockModification = GetWarningList().Contains(block);
            var answer = -1;
            if (dangerousBlockModification)
            {
                answer = (int) MessageBox.Show("This block contains keys and access conditions, modify it can make whole sector irreversibly blocked." );
            }

            int index;
            if (!dangerousBlockModification || answer != 0)
            {
                if (!AuthenticationRequired || AuthenticateBlock())
                {
                    ClearBuffers();
                    SendBuff[0] = 0xFF; // CLA - The instruction class
                    SendBuff[1] = 0xD6; // INS - The instruction code : Select File
                    SendBuff[2] = 0x00; // P1 - Select MF or EF using short ID
                    SendBuff[3] = (byte)block; // P2 : Starting Block No.
                    SendBuff[4] = (byte)int.Parse("16"); // P3 : Data length

                    for (index = 0; index <= tmpStr.Length - 1; index++)
                    {
                        SendBuff[index + 5] = (byte)tmpStr[index];
                    }
                    SendLen = SendBuff[4] + 5;
                    RecvLen = 0x02;

                    _retCode = SendAPDUandDisplay(RequestType.ReadingWriting); //Writing

                    if (_retCode != Card.SCARD_S_SUCCESS)
                    {
                        MessageBox.Show("fail write");

                    }
                    else if (_retCode == -203)
                    {
                        MessageBox.Show("Fail Authentication");
                    }
                    else
                    {
                        MessageBox.Show("write success");
                    }
                }
                else
                {
                    MessageBox.Show("Fail Authentication");
                }
            }
        }

        // block authentication
        private bool AuthenticateBlock()
        {
            var authenticationSuccess = LoadAuthenticationKey();

            ClearBuffers();
            SendBuff[0] = 0xFF; // CLA
            SendBuff[1] = 0x86; // INS: for stored key input
            SendBuff[2] = 0x00; // P1: same for all source types 
            SendBuff[3] = 0x00; // P2 : Memory location;  P2: for stored key input
            SendBuff[4] = 0x05; // P3: for stored key input
            SendBuff[5] = 0x01; // Byte 1: version number
            SendBuff[6] = 0x00; // Byte 2
            SendBuff[7] = (byte)SelectedBlock; // Byte 3: sectore no. for stored key input
            SendBuff[8] = 0x60; // Byte 4 : Key A for stored key input
            SendBuff[9] = (byte)int.Parse("1"); // Byte 5 : Session key for non-volatile memory

            SendLen = 0x0A;
            RecvLen = 0x02;

            _retCode = SendAPDUandDisplay(RequestType.Authentication);

            return authenticationSuccess && _retCode == Card.SCARD_S_SUCCESS;
        }

        private bool LoadAuthenticationKey()
        {
            ClearBuffers();
            const int keyByteLength = 6;
            SendBuff[0] = 0xFF; // CLA
            SendBuff[1] = 0x82; // INS: for stored key input
            SendBuff[2] = 0x00; // P1: same for all source types 
            SendBuff[3] = 0x01; // P2 : Memory location 0 or 1;
            SendBuff[4] = keyByteLength; // P3: Key Length
            for (int i = 0; i < AuthenticationKey.Length / 2; i++)
            {
                SendBuff[5 + i] = FromHexadecimalToByteArray(AuthenticationKey.Substring(i * 2, 2))[0];
            }

            SendLen = 5 + keyByteLength;
            RecvLen = 0x02;

            _retCode = SendAPDUandDisplay(RequestType.Authentication);

            if (_retCode != Card.SCARD_S_SUCCESS)
            {
                return false;
            }

            return true;
        }

        // clear memory buffers
        private void ClearBuffers()
        {
            for (long indx = 0; indx <= 262; indx++)
            {
                RecvBuff[indx] = 0;
                SendBuff[indx] = 0;
            }
        }

        // send application protocol data unit : communication unit between a smart card reader and a smart card
        private int SendAPDUandDisplay(RequestType reqType)
        {
            int index;
            var tmpStr = string.Empty;

            PioSendRequest.dwProtocol = Aprotocol;
            PioSendRequest.cbPciLength = 8;

            //Display Apdu In
            for (index = 0; index <= SendLen - 1; index++)
            {
                tmpStr += " " + string.Format("{0:X2}", SendBuff[index]);
            }

            _retCode = Card.SCardTransmit(_hCard, ref PioSendRequest, ref SendBuff[0],
                                 SendLen, ref PioSendRequest, ref RecvBuff[0], ref RecvLen);

            if (_retCode != Card.SCARD_S_SUCCESS)
            {
                return _retCode;
            }

            tmpStr = string.Empty;

            try
            {
                switch (reqType)
                {
                    case RequestType.Authentication:
                        for (index = (RecvLen - 2); index <= (RecvLen - 1); index++)
                        {
                            tmpStr += " " + string.Format("{0:X2}", RecvBuff[index]);
                        }

                        if (tmpStr.Trim() != "90 00")
                        {
                            return -202;
                        }

                        break;

                    //case 1:

                    //    for (index = (RecvLen - 2); index <= (RecvLen - 1); index++)
                    //    {
                    //        tmpStr += string.Format("{0:X2}", RecvBuff[index]);
                    //    }

                    //    if (tmpStr.Trim() != "90 00")
                    //    {
                    //        tmpStr += " " + string.Format("{0:X2}", RecvBuff[index]);
                    //    }

                    //    else
                    //    {
                    //        tmpStr = "ATR : ";
                    //        for (index = 0; index <= (RecvLen - 3); index++)
                    //        {
                    //            tmpStr += " " + string.Format("{0:X2}", RecvBuff[index]);
                    //        }
                    //    }

                    //    break;

                    case RequestType.ReadingWriting:

                        for (index = 0; index <= (RecvLen - 1); index++)
                        {
                            tmpStr += " " + string.Format("{0:X2}", RecvBuff[index]);
                        }

                        if (tmpStr.Trim() == "63 00")
                        {
                            return -203;
                        }

                        break;
                }
            }
            catch (IndexOutOfRangeException)
            {
                return -200;
            }

            return _retCode;
        }

        //disconnect card reader connection
        public void Disconnect()
        {
            if (ConnActive)
            {
                _retCode = Card.SCardDisconnect(_hCard, Card.SCARD_UNPOWER_CARD);
            }
        }

        public string VerifyCard(int block)
        {
            string value;
            if (ConnectCard())
            {
                value = ReadBlock(block);
            }
            else
            {
                return "Reading Failed";
            }

            return value.Split(new[] { '\0' }, 2, StringSplitOptions.None)[0];
        }

        public string ReadBlock(int block)
        {
            var tmpStr = string.Empty;

            if (!AuthenticationRequired || AuthenticateBlock())
            {
                ClearBuffers();
                SendBuff[0] = 0xFF; // Class
                SendBuff[1] = 0xB0;// INS
                SendBuff[2] = 0x00;// P1
                SendBuff[3] = (byte)block;// P2 : Block No.
                SendBuff[4] = (byte)int.Parse("16");// Length to read

                SendLen = 5;
                RecvLen = SendBuff[4] + 2;

                _retCode = SendAPDUandDisplay(RequestType.ReadingWriting); //Reading

                if (_retCode == -200)
                {
                    return "Out of range exception";
                }

                if (_retCode == -202)
                {
                    return "BytesNotAcceptable";
                }

                if (_retCode == -203) //Custom error code
                {
                    return "Authentication needed";
                }

                if (_retCode != Card.SCARD_S_SUCCESS)
                {
                    return "Fail Read";
                }

                // Display data in text format
                for (var index = 0; index <= RecvLen - 1; index++)
                {
                    tmpStr += Convert.ToChar(RecvBuff[index]);
                }

                return tmpStr;
            }

            return "Fail Authentication";
        }

        public void Read_Click()
        {
            //ReadValue = default;
            //ReadValue = VerifyCard(SelectedBlock);
        }

        public byte[] FromHexadecimalToByteArray(string hex)
        {
            hex = hex.Replace(" ", string.Empty);
            if (hex.Length % 2 == 1)
                hex = "0" + hex;
            byte[] hexaBytes;
            try { hexaBytes = Enumerable.Range(0, hex.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(hex.Substring(x, 2), 16)).ToArray(); }
            catch { return new byte[0]; }
            return hexaBytes;
        }

        private static List<int> GetBlockNumberList()
        {
            var list = new List<int>();
            for (int i = 0; i < 65; i++)
            {
                list.Add(i);
            }
            return list;
        }

        private static List<int> GetWarningList()
        {
            var list = new List<int> { 0 };
            for (int i = 3; i < 65; i += 4)
            {
                list.Add(i);
            }
            return list;
        }

























    }


    public class Card
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SCARD_IO_REQUEST
        {
            public int dwProtocol;
            public int cbPciLength;
        }

        //[StructLayout(LayoutKind.Sequential)]
        //public struct APDURec
        //{
        //    public byte bCLA;
        //    public byte bINS;
        //    public byte bP1;
        //    public byte bP2;
        //    public byte bP3;
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        //    public byte[] Data;
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        //    public byte[] SW;
        //    public bool IsSend;
        //}

        [StructLayout(LayoutKind.Sequential)]
        public struct SCARD_READERSTATE
        {
            public string RdrName;
            public int UserData;
            public int RdrCurrState;
            public int RdrEventState;
            public int ATRLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 37)]
            public byte[] ATRValue;
        }

        public const int SCARD_S_SUCCESS = 0;
        public const int SCARD_ATR_LENGTH = 33;


        /* ===========================================================
        '  Memory Card type constants
        '===========================================================*/
        public const int CT_MCU = 0x00;                   // MCU
        public const int CT_IIC_Auto = 0x01;               // IIC (Auto Detect Memory Size)
        public const int CT_IIC_1K = 0x02;                 // IIC (1K)
        public const int CT_IIC_2K = 0x03;                 // IIC (2K)
        public const int CT_IIC_4K = 0x04;                 // IIC (4K)
        public const int CT_IIC_8K = 0x05;                 // IIC (8K)
        public const int CT_IIC_16K = 0x06;                // IIC (16K)
        public const int CT_IIC_32K = 0x07;                // IIC (32K)
        public const int CT_IIC_64K = 0x08;                // IIC (64K)
        public const int CT_IIC_128K = 0x09;               // IIC (128K)
        public const int CT_IIC_256K = 0x0A;               // IIC (256K)
        public const int CT_IIC_512K = 0x0B;               // IIC (512K)
        public const int CT_IIC_1024K = 0x0C;              // IIC (1024K)
        public const int CT_AT88SC153 = 0x0D;              // AT88SC153
        public const int CT_AT88SC1608 = 0x0E;             // AT88SC1608
        public const int CT_SLE4418 = 0x0F;                // SLE4418
        public const int CT_SLE4428 = 0x10;                // SLE4428
        public const int CT_SLE4432 = 0x11;                // SLE4432
        public const int CT_SLE4442 = 0x12;                // SLE4442
        public const int CT_SLE4406 = 0x13;                // SLE4406
        public const int CT_SLE4436 = 0x14;                // SLE4436
        public const int CT_SLE5536 = 0x15;                // SLE5536
        public const int CT_MCUT0 = 0x16;                  // MCU T=0
        public const int CT_MCUT1 = 0x17;                  // MCU T=1
        public const int CT_MCU_Auto = 0x18;               // MCU Autodetect

        /*===============================================================
        ' CONTEXT SCOPE
        ===============================================================	*/
        public const int SCARD_SCOPE_USER = 0;
        /*===============================================================
        ' The context is a user context, and any database operations 
        '  are performed within the domain of the user.
        '===============================================================*/
        public const int SCARD_SCOPE_TERMINAL = 1;
        /*===============================================================
        ' The context is that of the current terminal, and any database 
        'operations are performed within the domain of that terminal.  
        '(The calling application must have appropriate access permissions 
        'for any database actions.)
        '===============================================================*/

        public const int SCARD_SCOPE_SYSTEM = 2;
        /*===============================================================
        ' The context is the system context, and any database operations 
        ' are performed within the domain of the system.  (The calling
        ' application must have appropriate access permissions for any 
        ' database actions.)
        '===============================================================*/
        /*=============================================================== 
        ' Context Scope
        '===============================================================*/
        public const int SCARD_STATE_UNAWARE = 0x00;
        /*===============================================================
        ' The application is unaware of the current state, and would like 
        ' to know. The use of this value results in an immediate return
        ' from state transition monitoring services. This is represented
        ' by all bits set to zero.
        '===============================================================*/
        public const int SCARD_STATE_IGNORE = 0x01;
        /*===============================================================
        ' The application requested that this reader be ignored. No other
        ' bits will be set.
        '===============================================================*/

        public const int SCARD_STATE_CHANGED = 0x02;
        /*===============================================================
        ' This implies that there is a difference between the state 
        ' believed by the application, and the state known by the Service
        ' Manager.When this bit is set, the application may assume a
        ' significant state change has occurred on this reader.
        '===============================================================*/

        public const int SCARD_STATE_UNKNOWN = 0x04;
        /*===============================================================
        ' This implies that the given reader name is not recognized by
        ' the Service Manager. If this bit is set, then SCARD_STATE_CHANGED
        ' and SCARD_STATE_IGNORE will also be set.
        '===============================================================*/
        public const int SCARD_STATE_UNAVAILABLE = 0x08;
        /*===============================================================
        ' This implies that the actual state of this reader is not
        ' available. If this bit is set, then all the following bits are
        ' clear.
        '===============================================================*/
        public const int SCARD_STATE_EMPTY = 0x10;
        /*===============================================================
        '  This implies that there is not card in the reader.  If this bit
        '  is set, all the following bits will be clear.
         ===============================================================*/
        public const int SCARD_STATE_PRESENT = 0x20;
        /*===============================================================
        '  This implies that there is a card in the reader.
         ===============================================================*/
        public const int SCARD_STATE_ATRMATCH = 0x40;
        /*===============================================================
        '  This implies that there is a card in the reader with an ATR
        '  matching one of the target cards. If this bit is set,
        '  SCARD_STATE_PRESENT will also be set.  This bit is only returned
        '  on the SCardLocateCard() service.
         ===============================================================*/
        public const int SCARD_STATE_EXCLUSIVE = 0x80;
        /*===============================================================
        '  This implies that the card in the reader is allocated for 
        '  exclusive use by another application. If this bit is set,
        '  SCARD_STATE_PRESENT will also be set.
         ===============================================================*/
        public const int SCARD_STATE_INUSE = 0x100;
        /*===============================================================
        '  This implies that the card in the reader is in use by one or 
        '  more other applications, but may be connected to in shared mode. 
        '  If this bit is set, SCARD_STATE_PRESENT will also be set.
         ===============================================================*/
        public const int SCARD_STATE_MUTE = 0x200;
        /*===============================================================
        '  This implies that the card in the reader is unresponsive or not
        '  supported by the reader or software.
        ' ===============================================================*/
        public const int SCARD_STATE_UNPOWERED = 0x400;
        /*===============================================================
        '  This implies that the card in the reader has not been powered up.
        ' ===============================================================*/

        public const int SCARD_SHARE_EXCLUSIVE = 1;
        /*===============================================================
        ' This application is not willing to share this card with other 
        'applications.
        '===============================================================*/
        public const int SCARD_SHARE_SHARED = 2;
        /*===============================================================
        ' This application is willing to share this card with other 
        'applications.
        '===============================================================*/
        public const int SCARD_SHARE_DIRECT = 3;
        /*===============================================================
        ' This application demands direct control of the reader, so it 
        ' is not available to other applications.
        '===============================================================*/

        /*===========================================================
        '   Disposition
        '===========================================================*/
        public const int SCARD_LEAVE_CARD = 0;   // Don't do anything special on close
        public const int SCARD_RESET_CARD = 1;   // Reset the card on close
        public const int SCARD_UNPOWER_CARD = 2;   // Power down the card on close
        public const int SCARD_EJECT_CARD = 3;   // Eject the card on close


        /* ===========================================================
        ' ACS IOCTL class
        '===========================================================*/
        public const long FILE_DEVICE_SMARTCARD = 0x310000; // Reader action IOCTLs

        public const long IOCTL_SMARTCARD_DIRECT = FILE_DEVICE_SMARTCARD + 2050 * 4;
        public const long IOCTL_SMARTCARD_SELECT_SLOT = FILE_DEVICE_SMARTCARD + 2051 * 4;
        public const long IOCTL_SMARTCARD_DRAW_LCDBMP = FILE_DEVICE_SMARTCARD + 2052 * 4;
        public const long IOCTL_SMARTCARD_DISPLAY_LCD = FILE_DEVICE_SMARTCARD + 2053 * 4;
        public const long IOCTL_SMARTCARD_CLR_LCD = FILE_DEVICE_SMARTCARD + 2054 * 4;
        public const long IOCTL_SMARTCARD_READ_KEYPAD = FILE_DEVICE_SMARTCARD + 2055 * 4;
        public const long IOCTL_SMARTCARD_READ_RTC = FILE_DEVICE_SMARTCARD + 2057 * 4;
        public const long IOCTL_SMARTCARD_SET_RTC = FILE_DEVICE_SMARTCARD + 2058 * 4;
        public const long IOCTL_SMARTCARD_SET_OPTION = FILE_DEVICE_SMARTCARD + 2059 * 4;
        public const long IOCTL_SMARTCARD_SET_LED = FILE_DEVICE_SMARTCARD + 2060 * 4;
        public const long IOCTL_SMARTCARD_LOAD_KEY = FILE_DEVICE_SMARTCARD + 2062 * 4;
        public const long IOCTL_SMARTCARD_READ_EEPROM = FILE_DEVICE_SMARTCARD + 2065 * 4;
        public const long IOCTL_SMARTCARD_WRITE_EEPROM = FILE_DEVICE_SMARTCARD + 2066 * 4;
        public const long IOCTL_SMARTCARD_GET_VERSION = FILE_DEVICE_SMARTCARD + 2067 * 4;
        public const long IOCTL_SMARTCARD_GET_READER_INFO = FILE_DEVICE_SMARTCARD + 2051 * 4;
        public const long IOCTL_SMARTCARD_SET_CARD_TYPE = FILE_DEVICE_SMARTCARD + 2060 * 4;
        public const long IOCTL_SMARTCARD_ACR128_ESCAPE_COMMAND = FILE_DEVICE_SMARTCARD + 2079 * 4;

        /*===========================================================
        '   Error Codes
        '===========================================================*/
        public const int SCARD_F_INTERNAL_ERROR = -2146435071;
        public const int SCARD_E_CANCELLED = -2146435070;
        public const int SCARD_E_INVALID_HANDLE = -2146435069;
        public const int SCARD_E_INVALID_PARAMETER = -2146435068;
        public const int SCARD_E_INVALID_TARGET = -2146435067;
        public const int SCARD_E_NO_MEMORY = -2146435066;
        public const int SCARD_F_WAITED_TOO_LONG = -2146435065;
        public const int SCARD_E_INSUFFICIENT_BUFFER = -2146435064;
        public const int SCARD_E_UNKNOWN_READER = -2146435063;


        public const int SCARD_E_TIMEOUT = -2146435062;
        public const int SCARD_E_SHARING_VIOLATION = -2146435061;
        //public const int SCARD_E_NO_SMARTCARD = -2146435060;
        public const int SCARD_E_NO_SMARTCARD = -2146434967;
        public const int SCARD_E_UNKNOWN_CARD = -2146435059;
        public const int SCARD_E_CANT_DISPOSE = -2146435058;
        public const int SCARD_E_PROTO_MISMATCH = -2146435057;


        public const int SCARD_E_NOT_READY = -2146435056;
        public const int SCARD_E_INVALID_VALUE = -2146435055;
        public const int SCARD_E_SYSTEM_CANCELLED = -2146435054;
        public const int SCARD_F_COMM_ERROR = -2146435053;
        public const int SCARD_F_UNKNOWN_ERROR = -2146435052;
        public const int SCARD_E_INVALID_ATR = -2146435051;
        public const int SCARD_E_NOT_TRANSACTED = -2146435050;
        public const int SCARD_E_READER_UNAVAILABLE = -2146435049;
        public const int SCARD_P_SHUTDOWN = -2146435048;
        public const int SCARD_E_PCI_TOO_SMALL = -2146435047;

        public const int SCARD_E_READER_UNSUPPORTED = -2146435046;
        public const int SCARD_E_DUPLICATE_READER = -2146435045;
        public const int SCARD_E_CARD_UNSUPPORTED = -2146435044;
        public const int SCARD_E_NO_SERVICE = -2146435043;
        public const int SCARD_E_SERVICE_STOPPED = -2146435042;

        public const int SCARD_W_UNSUPPORTED_CARD = -2146435041;
        public const int SCARD_W_UNRESPONSIVE_CARD = -2146435040;
        public const int SCARD_W_UNPOWERED_CARD = -2146435039;
        public const int SCARD_W_RESET_CARD = -2146435038;
        public const int SCARD_W_REMOVED_CARD = -2146435037;

        /*===========================================================
        '   PROTOCOL
        '===========================================================*/
        public const int SCARD_PROTOCOL_UNDEFINED = 0x00;          // There is no active protocol.
        public const int SCARD_PROTOCOL_T0 = 0x01;                 // T=0 is the active protocol.
        public const int SCARD_PROTOCOL_T1 = 0x02;                 // T=1 is the active protocol.
        public const int SCARD_PROTOCOL_RAW = 0x10000;             // Raw is the active protocol.
                                                                   //public const int SCARD_PROTOCOL_DEFAULT = 0x80000000;      // Use implicit PTS.
        /*===========================================================
        '   READER STATE
        '===========================================================*/
        public const int SCARD_UNKNOWN = 0;
        /*===============================================================
        ' This value implies the driver is unaware of the current 
        ' state of the reader.
        '===============================================================*/
        public const int SCARD_ABSENT = 1;
        /*===============================================================
        ' This value implies there is no card in the reader.
        '===============================================================*/
        public const int SCARD_PRESENT = 2;
        /*===============================================================
        ' This value implies there is a card is present in the reader, 
        'but that it has not been moved into position for use.
        '===============================================================*/
        public const int SCARD_SWALLOWED = 3;
        /*===============================================================
        ' This value implies there is a card in the reader in position 
        ' for use.  The card is not powered.
        '===============================================================*/
        public const int SCARD_POWERED = 4;
        /*===============================================================
        ' This value implies there is power is being provided to the card, 
        ' but the Reader Driver is unaware of the mode of the card.
        '===============================================================*/
        public const int SCARD_NEGOTIABLE = 5;
        /*===============================================================
        ' This value implies the card has been reset and is awaiting 
        ' PTS negotiation.
        '===============================================================*/
        public const int SCARD_SPECIFIC = 6;



        /*===============================================================
' This value implies the card has been reset and specific 
' communication protocols have been established.
'===============================================================*/

        /*==========================================================================
        ' Prototypes
        '==========================================================================*/


        [DllImport("winscard.dll")]
        public static extern int SCardEstablishContext(int dwScope, int pvReserved1, int pvReserved2, ref int phContext);

        [DllImport("winscard.dll")]
        public static extern int SCardReleaseContext(int phContext);

        [DllImport("winscard.dll")]
        public static extern int SCardConnect(int hContext, string szReaderName, int dwShareMode, int dwPrefProtocol, ref int phCard, ref int ActiveProtocol);

        [DllImport("winscard.dll")]
        public static extern int SCardBeginTransaction(int hCard);

        [DllImport("winscard.dll")]
        public static extern int SCardDisconnect(int hCard, int Disposition);

        [DllImport("winscard.dll")]
        public static extern int SCardListReaderGroups(int hContext, ref string mzGroups, ref int pcchGroups);

        [DllImport("winscard.DLL", EntryPoint = "SCardListReadersA", CharSet = CharSet.Ansi)]
        public static extern int SCardListReaders(
            int hContext,
            byte[] Groups,
            byte[] Readers,
            ref int pcchReaders
            );


        [DllImport("winscard.dll")]
        public static extern int SCardStatus(int hCard, string szReaderName, ref int pcchReaderLen, ref int State, ref int Protocol, ref byte ATR, ref int ATRLen);

        [DllImport("winscard.dll")]
        public static extern int SCardEndTransaction(int hCard, int Disposition);

        [DllImport("winscard.dll")]
        public static extern int SCardState(int hCard, ref uint State, ref uint Protocol, ref byte ATR, ref uint ATRLen);

        [DllImport("WinScard.dll")]
        public static extern int SCardTransmit(IntPtr hCard,
                                               ref SCARD_IO_REQUEST pioSendPci,
                                               ref byte[] pbSendBuffer,
                                               int cbSendLength,
                                               ref SCARD_IO_REQUEST pioRecvPci,
                                               ref byte[] pbRecvBuffer,
                                               ref int pcbRecvLength);

        [DllImport("winscard.dll")]
        public static extern int SCardTransmit(int hCard, ref SCARD_IO_REQUEST pioSendRequest, ref byte SendBuff, int SendBuffLen, ref SCARD_IO_REQUEST pioRecvRequest, ref byte RecvBuff, ref int RecvBuffLen);

        [DllImport("winscard.dll")]
        public static extern int SCardTransmit(int hCard, ref SCARD_IO_REQUEST pioSendRequest, ref byte[] SendBuff, int SendBuffLen, ref SCARD_IO_REQUEST pioRecvRequest, ref byte[] RecvBuff, ref int RecvBuffLen);

        [DllImport("winscard.dll")]
        public static extern int SCardControl(int hCard, uint dwControlCode, ref byte SendBuff, int SendBuffLen, ref byte RecvBuff, int RecvBuffLen, ref int pcbBytesReturned);

        [DllImport("winscard.dll")]
        public static extern int SCardGetStatusChange(int hContext, int TimeOut, ref SCARD_READERSTATE ReaderState, int ReaderCount);

        public static string GetScardErrMsg(int ReturnCode)
        {
            switch (ReturnCode)
            {
                case SCARD_E_CANCELLED:
                    return ("The action was canceled by an SCardCancel request.");
                case SCARD_E_CANT_DISPOSE:
                    return ("The system could not dispose of the media in the requested manner.");
                case SCARD_E_CARD_UNSUPPORTED:
                    return ("The smart card does not meet minimal requirements for support.");
                case SCARD_E_DUPLICATE_READER:
                    return ("The reader driver didn't produce a unique reader name.");
                case SCARD_E_INSUFFICIENT_BUFFER:
                    return ("The data buffer for returned data is too small for the returned data.");
                case SCARD_E_INVALID_ATR:
                    return ("An ATR string obtained from the registry is not a valid ATR string.");
                case SCARD_E_INVALID_HANDLE:
                    return ("The supplied handle was invalid.");
                case SCARD_E_INVALID_PARAMETER:
                    return ("One or more of the supplied parameters could not be properly interpreted.");
                case SCARD_E_INVALID_TARGET:
                    return ("Registry startup information is missing or invalid.");
                case SCARD_E_INVALID_VALUE:
                    return ("One or more of the supplied parameter values could not be properly interpreted.");
                case SCARD_E_NOT_READY:
                    return ("The reader or card is not ready to accept commands.");
                case SCARD_E_NOT_TRANSACTED:
                    return ("An attempt was made to end a non-existent transaction.");
                case SCARD_E_NO_MEMORY:
                    return ("Not enough memory available to complete this command.");
                case SCARD_E_NO_SERVICE:
                    return ("The smart card resource manager is not running.");
                case SCARD_E_NO_SMARTCARD:
                    return ("The operation requires a smart card, but no smart card is currently in the device.");
                case SCARD_E_PCI_TOO_SMALL:
                    return ("The PCI receive buffer was too small.");
                case SCARD_E_PROTO_MISMATCH:
                    return ("The requested protocols are incompatible with the protocol currently in use with the card.");
                case SCARD_E_READER_UNAVAILABLE:
                    return ("The specified reader is not currently available for use.");
                case SCARD_E_READER_UNSUPPORTED:
                    return ("The reader driver does not meet minimal requirements for support.");
                case SCARD_E_SERVICE_STOPPED:
                    return ("The smart card resource manager has shut down.");
                case SCARD_E_SHARING_VIOLATION:
                    return ("The smart card cannot be accessed because of other outstanding connections.");
                case SCARD_E_SYSTEM_CANCELLED:
                    return ("The action was canceled by the system, presumably to log off or shut down.");
                case SCARD_E_TIMEOUT:
                    return ("The user-specified timeout value has expired.");
                case SCARD_E_UNKNOWN_CARD:
                    return ("The specified smart card name is not recognized.");
                case SCARD_E_UNKNOWN_READER:
                    return ("The specified reader name is not recognized.");
                case SCARD_F_COMM_ERROR:
                    return ("An internal communications error has been detected.");
                case SCARD_F_INTERNAL_ERROR:
                    return ("An internal consistency check failed.");
                case SCARD_F_UNKNOWN_ERROR:
                    return ("An internal error has been detected, but the source is unknown.");
                case SCARD_F_WAITED_TOO_LONG:
                    return ("An internal consistency timer has expired.");
                case SCARD_S_SUCCESS:
                    return ("No error was encountered.");
                case SCARD_W_REMOVED_CARD:
                    return ("The smart card has been removed, so that further communication is not possible.");
                case SCARD_W_RESET_CARD:
                    return ("The smart card has been reset, so any shared state information is invalid.");
                case SCARD_W_UNPOWERED_CARD:
                    return ("Power has been removed from the smart card, so that further communication is not possible.");
                case SCARD_W_UNRESPONSIVE_CARD:
                    return ("The smart card is not responding to a reset.");
                case SCARD_W_UNSUPPORTED_CARD:
                    return ("The reader cannot communicate with the card, due to ATR string configuration conflicts.");
                default:
                    return ("?");
            }
        }
    }

}
