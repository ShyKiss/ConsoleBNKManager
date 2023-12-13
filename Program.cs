using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Dynamic;
using System.Data.Sql;
using System.Collections;
using System.Net;
using System.Threading;
using System.Data;
using System.CodeDom;

namespace BNKManager
{
    class Program
    {
        static void Main(string[] args)
        {
            var i = 0;
            //Console.WriteLine(args[0].ToString());
            // byte[] filebytes = File.ReadAllBytes("./741226639.wem
            WwiseBank myBank = null;
            string TargetBankName = null;
            foreach (string bnk in args)
            {
                if (Path.GetExtension(bnk) == ".bnk")
                {
                    myBank = new WwiseBank(bnk);
                    TargetBankName = Path.GetFileName(bnk);
                    break;
                }
            }
            if (myBank == null)
            {
                Console.WriteLine("Please, Drag .bnk file to me");
                Console.ReadKey();
                return;
            }
            args = args.Where((val => Path.GetExtension(val) == ".wem")).ToArray();
            if (args.Count() == 0)
            {
                int b = 0;
                if (Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "/wems"))
                {
                    foreach (string file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "wems"))
                    {
                        if(Path.GetExtension(file) == ".wem") {
                            args = args.Append(file).ToArray();
                            b++;
                        }
                    }
                }
                if (b == 0) {
                    args = null;
                }
            }

            Console.WriteLine("Scanning: " + TargetBankName + "\n-----------------------------------");
            double[] duration = { };
            foreach (BankSection banksection in myBank.bankSections)
            {
                if (banksection is DATASection)
                {
                    DATASection sect = banksection as DATASection;
                    foreach (DATASection.WEMFile file in sect.wemFiles)
                    {
                        duration = duration.Append(GetAudioFileSeconds(ref file.data)).ToArray();
                    }
                }
            }
            foreach (BankSection banksection in myBank.bankSections)
            {
                if (banksection is HIRCSection)
                {
                    HIRCSection sect = banksection as HIRCSection;
                    foreach (WwiseObject hh in sect.objects)
                    {
                        if (hh.objectType is WwiseObjectType.Sound_SFX__Sound_Voice)
                        {
                            SoundSFXVoiceWwiseObject obj = hh as SoundSFXVoiceWwiseObject;
                            Console.WriteLine($"DATA WEM: #{i+1}");
                            Console.WriteLine("ID: " + obj.ID.ToString());
                            Console.WriteLine("Audio File ID: " + obj.audioFileID.ToString());
                            Console.WriteLine("Sound Type: " + obj.soundType.ToString());
                            Console.WriteLine("Stream Type: " + obj.streamType.ToString());
                            Console.WriteLine("Duration: " + duration[i].ToString() + " sec\n");
                            i++;
                        }
                    }
                }
            }
            Console.WriteLine("Total Files: " + i.ToString() + "\n-----------------------------------");

            if (args == null)
            {
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Replacing: " + TargetBankName + "\n");
            foreach (string ifile in args)
            {
                try
                {
                    myBank = EditAudioFile(myBank, Convert.ToUInt32(Path.GetFileNameWithoutExtension(ifile)), File.ReadAllBytes(ifile));
                }
                catch (FormatException)
                {
                    Console.WriteLine("Error: Wrong .wem Name!");
                    Console.ReadKey();
                    return;
                }
                catch (OverflowException)
                {
                    Console.WriteLine("Error: Long .wem Name!");
                    Console.ReadKey();
                    return;
                }
                catch
                {
                    Console.WriteLine("Error: can't convert .wem");
                    Console.ReadKey();
                    return;
                }
            }

            if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "/Repacked Banks")) Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "/Repacked Banks");
            myBank.Save(AppDomain.CurrentDomain.BaseDirectory + "/Repacked Banks/" + TargetBankName);
            Console.ReadKey();
        }

        public static WwiseBank EditAudioFile(WwiseBank CurrentBank, uint fileID, byte[] newData)
        {
            var dataIndex = (DIDXSection)null;
            var data = (DATASection)null;
            var hirc = (HIRCSection)null;
            //WwiseBank myBank = new WwiseBank(AppDomain.CurrentDomain.BaseDirectory + @"/WwiseDefaultBank_VO_Patient_Sewers.bnk");
            foreach (BankSection Section in CurrentBank.bankSections) {
                if (Section is DIDXSection) dataIndex = (DIDXSection)Section;
                if (Section is DATASection) data = (DATASection)Section;
                if (Section is HIRCSection) hirc = (HIRCSection)Section;
            }
            if (dataIndex != null && data != null)
            {
                uint lastOffset = 0;
                for (int i = 0; i < dataIndex.embeddedWEMFiles.Count; i++)
                {
                    dataIndex.embeddedWEMFiles[i].offset = lastOffset;
                    if (dataIndex.embeddedWEMFiles[i].ID == fileID)
                    {
                        dataIndex.embeddedWEMFiles[i].length = (uint)newData.Length;
                    }
                    lastOffset += dataIndex.embeddedWEMFiles[i].length + 10;
                }

                for (int i = 0; i < data.wemFiles.Count; i++)
                {
                    //Console.WriteLine(data.wemFiles[i].info.ID.ToString());
                    if (data.wemFiles[i].info.ID == fileID)
                    {
                        Console.WriteLine($"Replaced: {fileID}.wem ({i+1})");
                        data.wemFiles[i].data = newData;
                    }

                }
                if (hirc != null)
                {
                    foreach (WwiseObject obj in hirc.objects)
                    {
                        if (obj.objectType == WwiseObjectType.Sound_SFX__Sound_Voice)
                        {
                            // Console.WriteLine("Replaced!");
                            SoundSFXVoiceWwiseObject soundObj = (SoundSFXVoiceWwiseObject)obj;
                            DIDXSection.EmbeddedWEM gotEmbedded = dataIndex.GetEmbeddedWEM(soundObj.audioFileID);
                            if (gotEmbedded != null)
                            {
                                soundObj.fileOffset = (uint)data.dataStartOffset + gotEmbedded.offset;
                                soundObj.fileLength = gotEmbedded.length;
                            }
                        }
                    }
                }
            }
            return CurrentBank;
            //myBank.Save(AppDomain.CurrentDomain.BaseDirectory + @"/WwiseDefaultBank_VO_Patient_Sewers2.bnk");
        }
        public static double GetAudioFileSeconds(ref byte[] data)
        {
            // Approximated
            double result = 0;
            using (BinaryReader br = new BinaryReader(new MemoryStream(data)))
            {
                br.BaseStream.Seek(4, SeekOrigin.Begin);
                float fileSize = br.ReadUInt32();
                br.BaseStream.Seek(28, SeekOrigin.Begin);
                float bytesPerSecond = br.ReadUInt32();
                float seconds = fileSize / bytesPerSecond;
                result = Math.Round(seconds, 3);
            }
            return result;
        }
    }
}
