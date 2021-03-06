﻿using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InnovatorAdmin
{
  public class WebServerBootstrapper : DefaultNancyBootstrapper
  {
    protected override void ConfigureConventions(Nancy.Conventions.NancyConventions nancyConventions)
    {
      base.ConfigureConventions(nancyConventions);
      nancyConventions.StaticContentsConventions.Add(GetResponse);
    }

    private Response GetResponse(NancyContext context, string rootPath)
    {
      var parts = context.Request.Path.TrimStart('/').Split('/');
      var window = Application.OpenForms.OfType<EditorWindow>().FirstOrDefault(e => e.Uid == parts[0]);
      if (window == null)
        return new Response().WithStatusCode(HttpStatusCode.NotFound);
      return window.GetResponse(context, rootPath);
    }
  }
}
