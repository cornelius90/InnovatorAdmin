﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace InnovatorAdmin
{
  public class AmlDiff
  {
    private enum KeyMode
    {
      Undefined,
      ItemId,
      ElementName
    }

    public static bool IsDifferent(string start, string dest)
    {
      return GetMergeScript(start, dest).Elements().Any();
    }

    public static XElement GetMergeScript(string start, string dest)
    {
      if (string.IsNullOrWhiteSpace(start))
        return new XElement("AML");

      var startElem = XElement.Parse(start);
      var result = new XElement(startElem.Name);

      // Create deletes
      if (string.IsNullOrEmpty(dest))
      {
        var itemTag = startElem.DescendantsAndSelf().First(e => e.Name.LocalName == "Item");
        var items = itemTag.Parent.Elements("Item").Where(e => e.Attribute("action") != null
          && (e.Attribute("action").Value == "merge" || e.Attribute("action").Value == "add"
            || e.Attribute("action").Value == "create"));
        XElement newItem;
        foreach (var item in items)
        {
          newItem = new XElement(item.Name, item.Attributes().Where(IsAttributeToCopy));
          newItem.SetAttributeValue("action", "delete");
        }
        return result;
      }

      // Create merges/deletes as necessary
      var destElem = XElement.Parse(dest);
      GetMergeScript(startElem, destElem, result);
      foreach (var elem in result.DescendantsAndSelf())
        elem.RemoveAnnotations<ElementKey>();
      return result;
    }

    private static void GetMergeScript(XElement start, XElement dest, XElement result)
    {
      var startList = SetKeys(start.Elements()).OrderBy(t => t.Annotation<ElementKey>().Key).ToArray();
      var destList = SetKeys(dest.Elements()).OrderBy(t => t.Annotation<ElementKey>().Key).ToArray();
      XElement res;

      startList.MergeSorted(destList, t => t.Annotation<ElementKey>().Key, (i, s, d) =>
      {
        switch (i)
        {
          case -1:
            res = EnsurePath(s, result);
            if (res.Name.LocalName == "Item")
            {
              res.SetAttributeValue("action", "delete");
            }
            else if (res.Name.LocalName != "Relationships")
            {
              res.SetAttributeValue("is_null", "1");
              res.Parent.SetAttributeValue("action", "edit");
            }
            break;
          case 1:
            res = EnsurePath(d, result);
            SetItemEdits(res.ReplaceWithElement(new XElement(d)));
            break;
          default:
            if (TextDiffers(s, d))
            {
              res = EnsurePath(s, result);
              SetItemEdits(res.ReplaceWithElement(new XElement(d)));
            }
            else
            {
              GetMergeScript(s, d, result);
            }
            break;
        }
      });
    }

    private static XElement SetItemEdits(XElement child)
    {
      foreach (var item in child.Parents().Where(e => e.Name.LocalName == "Item" && e.Attribute("action") == null))
      {
        item.SetAttributeValue("action", "edit");
      }
      return child;
    }

    private static bool TextDiffers(XElement x, XElement y)
    {
      // CData nodes inherit from XText, so this covers both
      var xText = x.Nodes().OfType<XText>().FirstOrDefault();
      var yText = y.Nodes().OfType<XText>().FirstOrDefault();

      if (xText == null && yText == null)
        return false;
      if (xText == null || yText == null)
        return true;
      return !string.Equals(xText.Value, yText.Value);
    }

    private static XElement EnsurePath(XElement path, XElement result)
    {
      var pathList = path.ParentsAndSelf().Reverse().ToArray();
      if (!pathList.Any())
        throw new ArgumentException();
      var curr = result;
      if (curr.Name != pathList[0].Name)
        throw new ArgumentException();

      XElement match;

      foreach (var elem in pathList.Skip(1))
      {
        match = curr.Elements().Where(e => PathMatches(elem, e)).FirstOrDefault();
        if (match == null)
        {
          match = new XElement(elem.Name, elem.Attributes().Where(IsAttributeToCopy));
          match.AddAnnotation(elem.Annotation<ElementKey>());
          curr.Add(match);
        }
        curr = match;
      }

      return curr;
    }

    private static bool IsAttributeToCopy(XAttribute a)
    {
      switch (a.Name.LocalName)
      {
        case "id":
        case "where":
        case "idlist":
        case "type":
        case XmlFlags.Attr_ConfigId:
        case "_keyed_name":
          return true;
      }
      return false;
    }

    private static bool PathMatches(XElement canonical, XElement compare)
    {
      if (!canonical.Name.Equals(compare.Name))
        return false;
      if (canonical.Name.LocalName == "Item")
      {
        if (canonical.Attribute("id") != null
          && (compare.Attribute("id") == null
          || compare.Attribute("id").Value != canonical.Attribute("id").Value)) return false;
        if (canonical.Attribute("where") != null
          && (compare.Attribute("where") == null
          || compare.Attribute("where").Value != canonical.Attribute("where").Value)) return false;
        if (canonical.Attribute("idlist") != null
          && (compare.Attribute("idlist") == null
          || compare.Attribute("idlist").Value != canonical.Attribute("idlist").Value)) return false;

        if (canonical.Attribute("id") == null
          && canonical.Attribute("where") == null
          && compare.Attribute("idlist") == null
          && canonical.Annotation<ElementKey>().Key != compare.Annotation<ElementKey>().Key)
          return false;
      }
      return true;
    }

    private static IEnumerable<XElement> SetKeys(IEnumerable<XElement> elems)
    {
      var mode = KeyMode.Undefined;
      var results = new List<Tuple<string, XElement>>();

      foreach (var elem in elems)
      {
        if (mode == KeyMode.Undefined)
        {
          if (elem.Name.LocalName == "Item"
            && (elem.Attribute("action") == null || elem.Attribute("action").Value != "get")
            && elem.Attribute("type") != null)
          {
            mode = KeyMode.ItemId;
          }
          else
          {
            mode = KeyMode.ElementName;
          }
        }

        elem.AddAnnotation(ElementKey.FromElement(elem, mode));
      }

      return elems;
    }

    private class ElementKey
    {
      public string Key { get; set; }

      private ElementKey(string key)
      {
        this.Key = key;
      }

      public static ElementKey FromElement(XElement elem, KeyMode mode)
      {
        if (mode == KeyMode.ItemId)
        {
          if (elem.Attribute("id") != null)
          {
            return new ElementKey(elem.Attribute("id").Value);
          }
          else if (elem.Attribute("idlist") != null)
          {
            return new ElementKey(elem.Attribute("idlist").Value);
          }
          else if (elem.Attribute("where") != null)
          {
            return new ElementKey(elem.Attribute("where").Value);
          }
          else if (elem.Element("related_id") != null)
          {
            var textNode = elem.Element("related_id").Nodes().OfType<XText>().FirstOrDefault();
            if (textNode == null)
            {
              var item = elem.Element("related_id").Element("Item");
              if (item != null && item.Attribute("id") != null)
                return new ElementKey(item.Attribute("id").Value);
            }
            else
            {
              return new ElementKey(textNode.Value);
            }
          }

          return new ElementKey(elem.Name.LocalName + "[" + ElementIndex(elem).ToString() + "]");
        }
        else
        {
          var lang = elem.Attribute(XNamespace.Xml + "lang");
          return new ElementKey(elem.Name.LocalName + (lang == null ? "" : "[" + lang.Value + "]"));
        }
      }

      private static int ElementIndex(XElement elem)
      {
        var curr = elem.PreviousNode;
        var i = 0;
        while (curr != null)
        {
          i++;
          curr = curr.PreviousNode;
        }
        return i;
      }
    }
  }
}
