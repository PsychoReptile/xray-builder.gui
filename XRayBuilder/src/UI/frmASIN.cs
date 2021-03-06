﻿using System;
using System.Windows.Forms;
using XRayBuilder.Core.DataSources.Amazon;

namespace XRayBuilderGUI.UI
{
    public partial class frmASIN : Form
    {
        public frmASIN()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (CheckAsin())
                Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData != Keys.Enter) return base.ProcessCmdKey(ref msg, keyData);
            if (CheckAsin())
                Close();
            return true;
        }

        private bool CheckAsin()
        {
            if (AmazonClient.IsAsin(tbAsin.Text))
                return true;

            MessageBox.Show(@"This does not appear to be a valid ASIN." +
                            Environment.NewLine +
                            @"Are you sure it is correct?", @"Invalid ASIN", MessageBoxButtons.RetryCancel,
                MessageBoxIcon.Error);
            return false;
        }
    }
}