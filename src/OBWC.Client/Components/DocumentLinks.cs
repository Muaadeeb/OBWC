using OBWC.Shared;

namespace OBWC.Client.Components;

public static class DocumentLinks
{
    public static string Page(DocumentCategory category) => category switch
    {
        DocumentCategory.Constitution => "/members/constitution",
        DocumentCategory.Budget => "/members/budget",
        DocumentCategory.Minutes => "/members/minutes",
        _ => "/documents"
    };

    public static string Label(DocumentCategory category) => category switch
    {
        DocumentCategory.Constitution => "Constitution",
        DocumentCategory.Budget => "Budget",
        DocumentCategory.Minutes => "Minutes",
        _ => category.ToString()
    };
}
