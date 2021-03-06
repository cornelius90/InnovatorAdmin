﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace InnovatorAdmin.Testing
{
  public class ParamAssign : ICommand
  {
    public string Comment { get; set; }
    public string Name { get; set; }
    public string Select { get; set; }
    public string Value { get; set; }

    public async Task Run(TestContext context)
    {
      if (!string.IsNullOrWhiteSpace(this.Select))
      {
        if (context.LastResult == null)
          throw new InvalidOperationException("Cannot assert a match when no query has been run");

        context.Parameters[this.Name] = XPathResult.Evaluate(context.LastResult, this.Select).ToString();
      }
      else if (string.IsNullOrEmpty(this.Value))
      {
        context.Parameters.Remove(this.Name);
      }
      else
      {
        context.Parameters[this.Name] = this.Value;
      }
    }
  }
}
