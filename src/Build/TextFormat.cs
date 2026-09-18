using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ModInfoPanel.Build
{
    /// <summary>zh-CN.json 风格：信息行用 “┃” 前缀，颜色块包裹；特点/来源为无前缀单行。</summary>
    internal static class Format
    {
        public const string Green = "#72d572";
        public const string Orange = "#ffa726";
        public const string Yellow = "#ffee58";
        public const string Blue = "#91a7ff";
        public const string Red = "#e84e40";
        public const string Feature = "orange";
        public const string Gray = "#9e9e9e";
        public const string Bar = "┃";

        public static string Col(string color, string text)
        {
            return "<color=" + color + ">" + text + "</color>";
        }

        /// <summary>无 “┃” 前缀的单行（特点 / 来源模组）。</summary>
        public static string Line(string color, string text)
        {
            return Col(color, text) + "\n";
        }

        public static string Block(string color, IList<string> lines)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<color=").Append(color).Append('>');
            bool first = true;
            foreach (string line in lines)
            {
                if (line == null)
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append('\n');
                }

                sb.Append(Bar).Append(line);
                first = false;
            }

            sb.Append("</color>");
            return sb.ToString();
        }

        public static void AppendBlock(StringBuilder sb, string color, IList<string> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return;
            }

            sb.Append(Block(color, lines)).Append('\n');
        }

        public static string Num(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static string Num(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static string Pct(float value)
        {
            return (value * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        public static string Pair(string label, string value)
        {
            return label + "：" + value;
        }

        /// <summary>效果数值行（wiki 风格：标签 空格 数值，如 “皮肤 +2.5”）。</summary>
        public static string Stat(string label, string value)
        {
            return label + " " + value;
        }
    }
}
