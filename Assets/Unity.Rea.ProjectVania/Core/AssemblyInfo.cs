using System.Runtime.CompilerServices;

// The grid's cells change only inside Core (Map.Set); the Core tests build custom grids through it.
[assembly: InternalsVisibleTo("Unity.Rea.ProjectVania.Core.Tests")]
