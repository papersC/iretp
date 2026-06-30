namespace IRETP.Domain.Enums;

/// <summary>
/// Building sustainability certification schemes surfaced in the ESG/
/// Sustainability module (RFP Section 20). Stored on ProjectCertification.
/// </summary>
public enum CertificationScheme
{
    Leed = 1,           // U.S. Green Building Council
    EstidamaPearl = 2,  // Abu Dhabi / UAE Urban Planning Council
    Breeam = 3,         // BRE Global UK
    WellHealthSafety = 4,
    MostadamKsa = 5
}

/// <summary>
/// Rating levels normalised across schemes. LEED: Certified/Silver/Gold/
/// Platinum. Estidama: 1–5 Pearl. BREEAM: Pass/Good/Very Good/Excellent/
/// Outstanding. Kept as an enum so the platform can apply a consistent
/// sort/colour scale on the map heatmap.
/// </summary>
public enum CertificationLevel
{
    Entry = 1,       // LEED Certified / Estidama 1 Pearl / BREEAM Pass
    Silver = 2,      // LEED Silver / Estidama 2 Pearl / BREEAM Good
    Gold = 3,        // LEED Gold / Estidama 3 Pearl / BREEAM Very Good
    Platinum = 4,    // LEED Platinum / Estidama 4 Pearl / BREEAM Excellent
    Exemplary = 5    // Estidama 5 Pearl / BREEAM Outstanding
}
