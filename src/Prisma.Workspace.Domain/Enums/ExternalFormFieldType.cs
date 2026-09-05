namespace Prisma.Workspace.Domain.Enums;

public enum ExternalFormFieldType
{
    Text = 1,
    TextArea = 2,
    Email = 3,
    Phone = 4,
    Select = 5,
    Number = 6,
    Boolean = 7,
    Date = 8,
    File = 9
}

public enum ExternalFormFieldKind
{
    Custom = 0,
    Subject = 1,
    DetailedDescription = 2,
    Category = 3,
    InformedPriority = 4,
    RequesterName = 5,
    RequesterEmail = 6,
    RequesterPhone = 7,
    RelatedService = 8,
    Attachments = 9
}

public enum ExternalFormRuleOperator
{
    Always = 0,
    Equals = 1,
    Contains = 2
}
