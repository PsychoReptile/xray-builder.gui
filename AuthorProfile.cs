﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace XRayBuilderGUI
{
    public class AuthorProfile
    {
        private Properties.Settings settings = Properties.Settings.Default;
        private frmMain main;

        private string ApPath = "";
        private BookInfo curBook = null;

        public string ApTitle = null;
        public Image ApAuthorImage = null;
        public string ApSubTitle = null;
        public string BioTrimmed = "";
        public List<BookInfo> otherBooks = new List<BookInfo>();
        public string authorImageUrl = "";
        public string authorAsin = "";

        public string EaSubTitle = null;

        public bool complete = false; //Set if constructor succeeded in generating profile

        public AuthorProfile(BookInfo nBook, string TLD, frmMain frm)
        {
            this.curBook = nBook;
            this.main = frm;
            string outputDir;
            try
            {
                if (settings.android)
                {
                    outputDir = settings.outDir + @"\Android\" + curBook.asin;
                    Directory.CreateDirectory(outputDir);
                }
                else
                    outputDir = settings.useSubDirectories ? Functions.GetBookOutputDirectory(curBook.author, curBook.sidecarName) : settings.outDir;
            }
            catch (Exception ex)
            {
                main.Log("An error occurred creating output directory: " + ex.Message + "\r\nFiles will be placed in the default output directory.");
                outputDir = settings.outDir;
            }
            ApPath = outputDir + @"\AuthorProfile.profile." + curBook.asin + ".asc";

            if (!Properties.Settings.Default.overwrite && File.Exists(ApPath))
            {
                main.Log("AuthorProfile file already exists... Skipping!\r\n" +
                         "Please review the settings page if you want to overwite any existing files.");
                return;
            }
            
            //Process GUID. If in decimal form, convert to hex.
            if (Regex.IsMatch(curBook.guid, "/[a-zA-Z]/"))
                curBook.guid = curBook.guid.ToUpper();
            else
            {
                long guidDec;
                long.TryParse(curBook.guid, out guidDec);
                curBook.guid = guidDec.ToString("X");
            }
            if (curBook.guid == "0")
            {
                main.Log("An error occurred while converting the GUID.");
                return;
            }

            DataSources.AuthorSearchResults searchResults = null;
            // Attempt to download from the alternate site, if present. If it fails in some way, try .com
            // If the .com search crashes, it will crash back to the caller in frmMain
            try
            {
                searchResults = DataSources.Amazon.SearchAuthor(curBook, TLD, main.Log);
            }
            catch (Exception ex)
            {
                main.Log("Error searching Amazon." + TLD + ": " + ex.Message);
            }
            finally
            {
                if (searchResults == null)
                {
                    main.Log("Failed to find author on Amazon." + TLD + ", trying again with Amazon.com.");
                    TLD = "com";
                    searchResults = DataSources.Amazon.SearchAuthor(curBook, TLD, main.Log);
                }
            }
            if (searchResults == null)
                return; // Already logged error in search function
            authorAsin = searchResults.authorAsin;

            if (Properties.Settings.Default.saveHtml)
            {
                try
                {
                    main.Log("Saving author's Amazon webpage...");
                    File.WriteAllText(Environment.CurrentDirectory + String.Format(@"\dmp\{0}.authorpageHtml.txt", curBook.asin),
                        searchResults.authorHtmlDoc.DocumentNode.InnerHtml);
                }
                catch (Exception ex)
                {
                    main.Log(String.Format("An error occurred saving authorpageHtml.txt: {0}", ex.Message));
                }
            }

            // Try to find author's biography
            string bioFile = Environment.CurrentDirectory + @"\ext\" + authorAsin + ".bio";
            if (settings.saveBio && File.Exists(bioFile))
            {
                if (!readBio(bioFile)) return;
            }
            if (BioTrimmed == "")
            {
                HtmlNode bio = DataSources.Amazon.GetBio(searchResults, TLD);
                //Trim authour biography to less than 1000 characters and/or replace more problematic characters.
                if (bio == null || bio.InnerText.Trim().Length != 0)
                {
                    if (bio.InnerText.Length > 1000)
                    {
                        int lastPunc = bio.InnerText.LastIndexOfAny(new char[] { '.', '!', '?' });
                        int lastSpace = bio.InnerText.LastIndexOf(' ');
                        if (lastPunc > lastSpace)
                            BioTrimmed = bio.InnerText.Substring(0, lastPunc + 1);
                        else
                            BioTrimmed = bio.InnerText.Substring(0, lastSpace) + '\u2026';
                    }
                    else
                    {
                        BioTrimmed = bio.InnerText;
                    }
                    BioTrimmed = Functions.CleanString(BioTrimmed);
                    main.Log("Author biography found on Amazon!");
                }
                else
                {
                    BioTrimmed = "No author biography found on Amazon!";
                    main.Log("An error occurred finding the author biography on Amazon.");
                }
            }
            if (settings.saveBio)
            {
                if (!File.Exists(bioFile))
                {
                    try
                    {
                        main.Log("Saving biography to " + bioFile);
                        using (var streamWriter = new StreamWriter(bioFile, false, System.Text.Encoding.UTF8))
                        {
                            streamWriter.Write(BioTrimmed);
                        }
                    }
                    catch (Exception ex)
                    {
                        main.Log("An error occurred while writing biography.\r\n" + ex.Message);
                        return;
                    }
                }
                if (System.Windows.Forms.DialogResult.Yes == System.Windows.Forms.MessageBox.Show("Would you like to open the biography file in notepad for editing?", "Biography",
                   System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Question, System.Windows.Forms.MessageBoxDefaultButton.Button2))
                {
                    Functions.RunNotepad(bioFile);
                    if (!readBio(bioFile)) return;
                }
            }
            // Try to download Author image
            HtmlNode imageXpath = DataSources.Amazon.GetAuthorImage(searchResults, TLD);
            authorImageUrl = imageXpath.GetAttributeValue("src", "");
            string downloadedAuthorImage = curBook.path + @"\DownloadedAuthorImage.jpg";
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.DownloadFile(new Uri(authorImageUrl), downloadedAuthorImage);
                    main.Log("Downloading author image...");
                }
            }
            catch (Exception ex)
            {
                main.Log(String.Format("An error occurred downloading the author image: {0}", ex.Message));
                return;
            }

            main.Log("Resizing and cropping Author image...");
            //Resize and Crop Author image
            Bitmap o = (Bitmap) Image.FromFile(downloadedAuthorImage);
            Bitmap nb = new Bitmap(o, o.Width, o.Height);

            int sourceWidth = o.Width;
            int sourceHeight = o.Height;
            float nPercent;
            float nPercentW = (185/(float) sourceWidth);
            float nPercentH = (278/(float) sourceHeight);

            nPercent = nPercentH > nPercentW ? nPercentH : nPercentW;

            int destWidth = (int) (sourceWidth*nPercent);
            int destHeight = (int) (sourceHeight*nPercent);
            Bitmap b = new Bitmap(destWidth, destHeight);
            Graphics g = Graphics.FromImage(b);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.CompositingMode = CompositingMode.SourceOver;

            ImageAttributes ia = new ImageAttributes();
            ia.SetWrapMode(WrapMode.TileFlipXY);

            g.DrawImage(nb, 0, 0, destWidth, destHeight);
            b.Save(curBook.path + @"\ResizedAuthorImage.jpg");
            b.Dispose();
            g.Dispose();
            o.Dispose();
            nb.Dispose();

            Bitmap target = new Bitmap(185, destHeight);
            Rectangle cropRect = new Rectangle(((destWidth - 185)/2), 0, 185, destHeight);
            using (g = Graphics.FromImage(target))
            {
                g.DrawImage(Image.FromFile(curBook.path + @"\ResizedAuthorImage.jpg"),
                    new Rectangle(0, 0, target.Width, target.Height),
                    cropRect, GraphicsUnit.Pixel);
            }
            target.Save(curBook.path + @"\CroppedAuthorImage.jpg");
            target.Dispose();
            Bitmap bc = new Bitmap(curBook.path + @"\CroppedAuthorImage.jpg");

            //Convert Author image to Grayscale and save as jpeg
            Bitmap bgs = Functions.MakeGrayscale3(bc);

            ImageCodecInfo[] availableCodecs = ImageCodecInfo.GetImageEncoders();
            ImageCodecInfo jpgCodec = availableCodecs.FirstOrDefault(codec => codec.MimeType == "image/jpeg");
            if (jpgCodec == null)
                throw new NotSupportedException("Encoder for JPEG not found.");
            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.ColorDepth, 8L);
            bgs.Save(curBook.path + @"\FinalImage.jpg", jpgCodec, encoderParams);
            int authorImageHeight = bgs.Height;
            bc.Dispose();

            //Convert final grayscale Author image to Base64 Format String
            string base64ImageString = Functions.ImageToBase64(bgs, ImageFormat.Jpeg);
            main.Log("Grayscale Base-64 encoded author image created!");
            bgs.Dispose();

            main.Log("Gathering author's other books...");
            List<BookInfo> bookList = DataSources.Amazon.GetAuthorBooks(searchResults, curBook.title, curBook.author, TLD);
            if (bookList != null)
            {
                main.Log("Gathering metadata for other books...");
                foreach (BookInfo book in bookList)
                {
                    try
                    {
                        //Gather book desc, image url, etc, if using new format
                        if (settings.useNewVersion)
                            book.GetAmazonInfo(book.amazonUrl);
                        otherBooks.Add(book);
                    }
                    catch (Exception ex)
                    {
                        main.Log(String.Format("An error occurred gathering metadata for other books: {0}\r\nURL: {1}\r\nBook: {2}", ex.Message, book.amazonUrl, book.title));
                        return;
                    }
                }
            }
            else
            {
                main.Log("Unable to find other books by this author. If there should be some, check the Amazon URL to ensure it is correct.");
            }

            main.Log("Writing Author Profile to file...");

            //Create list of Asin numbers and titles
            List<string> authorsOtherBookList = new List<string>();
            foreach (BookInfo bk in otherBooks)
            {
                authorsOtherBookList.Add(String.Format(@"{{""e"":1,""a"":""{0}"",""t"":""{1}""}}",
                    bk.asin, bk.title));
            }

            //Create finalAuthorProfile.profile.ASIN.asc
            int unixTimestamp = (Int32) (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            try
            {
                string authorProfileOutput = @"{""u"":[{""y"":" + authorImageHeight + @",""l"":[""" +
                                          string.Join(@""",""", otherBooks.Select(book => book.asin).ToArray()) + @"""],""n"":""" +
                                          curBook.author + @""",""a"":""" + authorAsin + @""",""b"":""" + BioTrimmed +
                                          @""",""i"":""" + base64ImageString + @"""}],""a"":""" +
                                          String.Format(@"{0}"",""d"":{1},""o"":[", curBook.asin, unixTimestamp) +
                                          string.Join(",", authorsOtherBookList.ToArray()) + "]}";
                File.WriteAllText(ApPath, authorProfileOutput);
                main.btnPreview.Enabled = true;
                main.cmsPreview.Items[0].Enabled = true;
                main.Log("Author Profile file created successfully!\r\nSaved to " + ApPath);
            }
            catch (Exception ex)
            {
                main.Log("An error occurred while writing the Author Profile file: " + ex.Message);
                return;
            }

            ApTitle = "About " + curBook.author;
            ApSubTitle = "Kindle Books By " + curBook.author;
            ApAuthorImage = Image.FromFile(curBook.path + @"\FinalImage.jpg");
            EaSubTitle = "More Books By " + curBook.author;

            complete = true;
        }

        public string ToJSON()
        {
            string template = @"{{""class"":""authorBio"",""asin"":""{0}"",""name"":""{1}"",""bio"":""{2}"",""imageUrl"":""{3}""}}";
            return Functions.ExpandUnicode(String.Format(template, authorAsin, curBook.author, BioTrimmed, authorImageUrl));
        }

        private bool readBio(string bioFile)
        {
            try
            {
                using (StreamReader streamReader = new StreamReader(bioFile, System.Text.Encoding.UTF8))
                {
                    BioTrimmed = streamReader.ReadToEnd();
                    if (BioTrimmed == "")
                        main.Log("Found biography file, but it is empty!\r\n" + bioFile);
                    else
                        main.Log("Using biography from " + bioFile + ".");
                }
            }
            catch (Exception ex)
            {
                main.Log("An error occurred while opening " + bioFile + "\r\n" + ex.Message);
                return false;
            }
            return true;
        }
    }
}