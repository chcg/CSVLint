﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using CSVLint;
using CSVLintNppPlugin.Forms;
using CsvQuery.PluginInfrastructure;
using Kbg.NppPluginNET.PluginInfrastructure;

namespace Kbg.NppPluginNET
{
    public partial class CsvLintWindow : Form
    {
        public CsvLintWindow()
        {
            InitializeComponent();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            // interface to Notepad++
            ScintillaGateway scintillaGateway = PluginBase.CurrentScintillaGateway;

            // get text
            var textLength = scintillaGateway.GetTextLength();
            //string lines = scintillaGateway.GetText(Math.Min(1000000, textLength)); // TODO sample size as settings
            string lines = scintillaGateway.GetText(textLength);

            // analyze and determine csv definition
            CsvDefinition csvdef = CsvAnalyze.InferFromData(lines);

            // display csv definition
            txtSchemaIni.Text = csvdef.getIniLines();
            txtOutput.Clear();
        }

        private CsvDefinition getCurrentCsvDef()
        {
            CsvDefinition csvdef;

            // get csv definition from ini lines
            string inilines = txtSchemaIni.Text;

            // get key values from  ini lines
            var enstr = inilines.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
               .Select(part => part.Split('='));

            // check for duplicate keys before turning into dictionary
            var dup = enstr.GroupBy(x => x[0])
                          .Where(g => g.Count() > 1)
                          .Select(y => y.Key)
                          .ToList();
            if (dup.Count > 0)
            {
                string errmsg = string.Format("Duplicate key(s) found ({0})", string.Join(",", dup));
                //throw new System.ArgumentException(err, "Error");
                //throw new ArgumentOutOfRangeException("error 123", err);
                MessageBox.Show(errmsg, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);

                csvdef = new CsvDefinition();
            }
            else
            {
                // create dictionary
                Dictionary<String, String> keys = enstr.ToDictionary(split => split[0], split => split[1]);

                // create dictionary
                csvdef = new CsvDefinition(keys);
            }
            return csvdef;
        }

        private void btnValidate_Click(object sender, EventArgs e)
        {
            string test = Main.Settings.NullValue;

            if (test == "NULL")
            {
                Debug.WriteLine("wel NULL");
            }
            else
            {
                Debug.WriteLine("niet NULL");
            }

            // interface to Notepad++
            ScintillaGateway scintillaGateway = PluginBase.CurrentScintillaGateway;

            // get sample of text
            var textLength = scintillaGateway.GetTextLength();
            //string lines = scintillaGateway.GetTextRange(Math.Min(100000, textLength));
            string sample = scintillaGateway.GetText(Math.Min(100000, textLength));

            // get dictionary
            CsvDefinition csvdef = getCurrentCsvDef();

            // check if valid dictionary
            if (csvdef.Fields.Count > 0)
            {
                // validate data
                CsvValidate csvval = new CsvValidate();

                var sr = ScintillaStreams.StreamAllText();

                //csvval.ValidateData(sample, csvdef);
                csvval.ValidateData(sr, csvdef);

                // display output message or errors
                string msg = csvval.report();
                txtOutput.Text = msg;
            }
        }

        private void txtOutput_DoubleClick(object sender, EventArgs e)
        {
            // double click on text box to jump to line in editor, 
            // Note: should fix this q&d solution, log and ui should be properly decoupled

            // variables
            int linenumber = 0;
            TextBox log = (sender as TextBox);
            String errval = "";

            // determine current line number in textbox
            int lineNumber = log.GetLineFromCharIndex(log.SelectionStart);
            int position = log.GetFirstCharIndexFromLine(lineNumber);

            // error check
            if (position >= 0)
            {
                // log line always "** error line 123: error in.. etc.", check for ':'
                int lineEnd = log.Text.IndexOf(':', position);
                if (lineEnd >= 0)
                {
                    // get part between "line" and ":"
                    position += 13; // "** error line " is 14 characters
                    string logline = log.Text.Substring(position, lineEnd - position);

                    Int32.TryParse(logline, out linenumber);

                    // find error value
                    position = log.Text.IndexOf(" value \"", lineEnd);
                    if (position >= 0)
                    {
                        position += 8; // " value \"" is 8 characters
                        lineEnd = log.Text.IndexOf('"', position);
                        if (lineEnd >= 0)
                        {
                            errval = log.Text.Substring(position, lineEnd - position);
                        }
                    }
                }
            }

            // editor jump to line
            if (linenumber > 0)
            {
                // interface to Notepad++
                ScintillaGateway scintillaGateway = PluginBase.CurrentScintillaGateway;
                scintillaGateway.GotoLine(linenumber - 1); // zero-based index

                // set target to current line
                scintillaGateway.SetTargetStart(Math.Max(scintillaGateway.GetCurrentPos(), scintillaGateway.GetAnchor()));
                scintillaGateway.SetTargetEnd(scintillaGateway.GetLineEndPosition(linenumber - 1));

                // search in line and SetSelection to the error value
                int selpos = scintillaGateway.SearchInTarget(errval.Length, errval);
                if (selpos != -1) {
                    scintillaGateway.SetSelection(selpos, selpos + errval.Length);
                }
            }
        }
        private bool getReformatParameters(out string dt, out string dec, out string sep, out bool updsep)
        {
            // show reformat form
            var frmedit = new ReformatForm();
            frmedit.InitialiseSetting("yyyy-MM-dd hh:mm:ss", ".", "|");
            DialogResult r = frmedit.ShowDialog();

            // user clicked OK or Cancel
            dt = frmedit.newDataTime;
            dec = frmedit.newDecimal;
            sep = frmedit.newSeparator;
            updsep = frmedit.updateSeparator;

            // clear up
            frmedit.Dispose();

            // return true (OK) or false (Cancel)
            return (r == DialogResult.OK);
        }

        private void btnReformat_Click(object sender, EventArgs e)
        {
            string editDataTime;
            string editDecimal;
            string editSeparator;
            bool updateSeparator;

            bool ok = getReformatParameters(out editDataTime, out editDecimal, out editSeparator, out updateSeparator);
            if (ok)
            {
                // get dictionary
                CsvDefinition csvdef = getCurrentCsvDef();

                // analyze and determine csv definition
                CsvEdit.ReformatDataFile(csvdef, editDataTime, editDecimal, editSeparator, updateSeparator);

                String msg = "";

                // refresh datadefinition
                if (editDataTime != "")
                {
                    msg += String.Format("Reformat datetime format from \"{0}\" to \"{1}\"\r\n", csvdef.DateTimeFormat, editDataTime);
                    csvdef.DateTimeFormat = editDataTime;
                };

                if (editDecimal != "")
                {
                    msg += String.Format("Reformat decimal separator from {0} to {1}\r\n", csvdef.DecimalSymbol, editDecimal);
                    csvdef.DecimalSymbol = editDecimal[0];
                };

                if (updateSeparator)
                {
                    string oldsep = (csvdef.Separator == '\0' ? "{Fixed width}" : (csvdef.Separator == '\t' ? "{Tab}" : csvdef.Separator.ToString()));
                    string newsep = (editSeparator == "\0" ? "{Fixed width}" : (editSeparator == "\t" ? "{Tab}" : editSeparator));

                    // fixed width doesn't contain header line (for example, columns can be width=1, no room in header line for column name longer than 1 character)
                    if (editSeparator == "\0")
                    {
                        csvdef.ColNameHeader = false;
                    }

                    msg += String.Format("Reformat column separator from {0} to {1}\r\n", oldsep, newsep);
                    csvdef.Separator = editSeparator[0];


                };

                // display process message
                msg += "Reformatting of data is ready.\r\n";
                txtOutput.Text = msg;

                // refresh datadefinition
                txtSchemaIni.Text = csvdef.getIniLines();
            }
        }
    }
}
