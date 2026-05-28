using System.IO;
using System.Text;
using HtmlAgilityPack;
using ReverseMarkdown;
using ReverseMarkdown.Converters;

namespace DocumentTranslator.Services;

internal sealed class HtmlBlockPassthroughConverter : ConverterBase
{
    private readonly string _tagName;
    private readonly bool _recurseChildren;

    public HtmlBlockPassthroughConverter(Converter converter, string tagName, bool recurseChildren)
        : base(converter)
    {
        _tagName = tagName;
        _recurseChildren = recurseChildren;
        converter.Register(tagName, this);
    }

    public override void Convert(TextWriter writer, HtmlNode node)
    {
        var openTag = new StringBuilder("<").Append(_tagName);
        foreach (var attr in node.Attributes)
        {
            openTag.Append(' ').Append(attr.Name).Append("=\"").Append(attr.Value).Append('"');
        }
        openTag.Append('>');

        var closeTag = "</" + _tagName + ">";

        if (_recurseChildren)
        {
            writer.Write(Environment.NewLine);
            writer.Write(Environment.NewLine);
            writer.Write(openTag.ToString());
            writer.Write(Environment.NewLine);
            writer.Write(Environment.NewLine);

            using var inner = new StringWriter();
            TreatChildren(inner, node);
            writer.Write(inner.ToString().Trim());

            writer.Write(Environment.NewLine);
            writer.Write(Environment.NewLine);
            writer.Write(closeTag);
            writer.Write(Environment.NewLine);
            writer.Write(Environment.NewLine);
        }
        else
        {
            writer.Write(openTag.ToString());
            writer.Write(node.InnerHtml);
            writer.Write(closeTag);
        }
    }
}

