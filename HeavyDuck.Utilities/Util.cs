using System;
using System.Collections.Generic;
using System.Text;

namespace HeavyDuck.Utilities
{
    public static class Util
    {
        public static TEnum EnumParse<TEnum>(string value)
        {
            return (TEnum)Enum.Parse(typeof(TEnum), value);
        }
    }
}
