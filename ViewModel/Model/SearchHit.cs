using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADLMRateGen.ViewModel.Model
{
	// Models/SearchHit.cs
	public record SearchHit(string Display,
							string Category,          // e.g. "Material", "Labour", …
							Action Navigate);         // what to run when picked

}
