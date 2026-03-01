// InvestmentCompanyLibrary.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "CompanyLibrary", menuName = "Scriptable Objects/Company Library")]
public class InvestmentCompanyLibrary : ScriptableObject
{
    public List<InvestmentCompany> companies;
}