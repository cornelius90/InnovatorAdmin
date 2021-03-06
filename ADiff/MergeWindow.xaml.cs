﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ADiff
{
  /// <summary>
  /// Interaction logic for MergeWindow.xaml
  /// </summary>
  public partial class MergeWindow : Window
  {
    public MergeWindow()
    {
      InitializeComponent();

      var engine = new DiffMatchPatch();
      var path = @"C:\Users\edomke\Documents\Local_Projects\ArasImportExport\DiffTests\Misc.java";
      var result = engine.diff_three_way(System.IO.File.ReadAllText(path + ".parent"),
        System.IO.File.ReadAllText(path + ".1st"),
        System.IO.File.ReadAllText(path + ".2nd"));
      merge.Document = result;

    }
  }
}
