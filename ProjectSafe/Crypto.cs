using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace ProjectSafe
{
    internal class Crypto
    {                                                                                              
        private const string BackupSuffix = ".backup";

        private readonly RijndaelManaged _aes;

        public Crypto(string password)
        {
            var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(password));
            var md5 = MD5.Create();
            var salt = md5.ComputeHash(md5.ComputeHash(Encoding.ASCII.GetBytes(password)));
            _aes = new RijndaelManaged
            {
                Padding = PaddingMode.PKCS7,
                Mode = CipherMode.CBC
            };

            using (var pbkdf2 = new Rfc2898DeriveBytes(hash.BinToHex(), salt, 50000))
            {
                _aes.Key = pbkdf2.GetBytes(_aes.KeySize / 8);
                _aes.IV = pbkdf2.GetBytes(_aes.BlockSize / 8);
            }
        }

        ~Crypto()
        {
            _aes.Dispose();
        }

        public void Encrypt(string file, bool backup)
        {
            if (file.EndsWith(BackupSuffix))
                return;
            try
            {
                using (var mem = new MemoryStream())
                {
                    using (var enc = _aes.CreateEncryptor())
                    {
                        using (var cs = new CryptoStream(mem, enc, CryptoStreamMode.Write))
                        {
                            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                            {
                                var input = new byte[fs.Length];
                                fs.Read(input, 0, input.Length);
                                cs.Write(input, 0, input.Length);
                                cs.FlushFinalBlock();
                            }

                            var buf = new byte[mem.Length];
                            mem.Seek(0, SeekOrigin.Begin);
                            mem.Read(buf, 0, buf.Length);
                            var sec = buf.BinToHex();
                            if (backup)
                            {
                                File.Copy(file, file + BackupSuffix);    
                            }

                            using (var writer = new StreamWriter(new FileStream(file, FileMode.Truncate)))
                            {
                                writer.Write(sec);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        public void Decrypt(string file, bool backup)
        {
            if (file.EndsWith(BackupSuffix))
                return;
            try
            {
                byte[] input;
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    input = new byte[fs.Length];
                    fs.Read(input, 0, input.Length);
                }

                var sec = Encoding.ASCII.GetString(input).HexToBin();
                using (var mem = new MemoryStream(sec))
                {
                    using (var dec = _aes.CreateDecryptor())
                    {
                        using (var cs = new CryptoStream(mem, dec, CryptoStreamMode.Read))
                        {
                            var buf = new byte[mem.Length];
                            var len = cs.Read(buf, 0, buf.Length);
                            if (backup)
                            {
                                File.Copy(file, file + BackupSuffix);
                            }            
                                                     
                            using (var f = new FileStream(file, FileMode.Truncate))
                            {
                                f.Write(buf, 0, len);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
    }
}

internal static class HexExtersion
{
    public static string BinToHex(this byte[] hash)
    {
        if (hash == null || hash.Length == 0)
            return null;
        var sb = new StringBuilder();
        hash.ToList().ForEach(x => sb.Append($"{x:X2}"));
        return sb.ToString();
    }

    public static byte[] HexToBin(this string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return null;
        var bb = new byte[hex.Length / 2];
        for (var i = 0; i < hex.Length / 2; ++i)
        {
            bb[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bb;
    }
}