namespace LoDb.Ingestion.Catalog;

/// <summary>
/// Who asks for a catalog no instance memory holds yet, which decides how its load is
/// admitted (<c>LoDb:Catalog:ColdLoad*</c>).
/// </summary>
/// <remarks>
/// Distinct from <see cref="Queue.OnDemandOrigin"/>, which decides what an ingestion may cost:
/// this one only splits the loads of stored catalogs between the site's own renderer and the
/// rest of the world, so that a client sweeping versions never starves the pages.
/// </remarks>
public enum ColdCaller
{
    /// <summary>
    /// A call through the reverse proxy: a browser, an app, a script. Admitted at a low rate,
    /// refused at once rather than queued.
    /// </summary>
    Public,

    /// <summary>
    /// The server-side renderer, or a task of the instance: a higher rate, and a short wait
    /// for its turn.
    /// </summary>
    Internal,
}
