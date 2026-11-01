using System;
using System.Collections.Generic;

namespace Unity.Rea.ProjectVania.Core.Characters
{
    /// <summary>
    /// <c>g.RandomName</c> (RECONSTRUCTION 4.1, 14): a uniform pick from names.json's table for the character's gender.
    /// BIBLE defect 8 fixed: every name in the table can be picked. The original drew <c>Next(Length - 1)</c>, so its
    /// last name never came up.
    /// </summary>
    public static class RandomName
    {
        public static string Pick(IReadOnlyList<string> names, Random random)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (names.Count == 0) throw new ArgumentException("A name table holds at least one name.", nameof(names));
            return names[random.Next(names.Count)];
        }
    }
}
