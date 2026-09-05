export interface AuditLogItem {
  id:string;userId?:string|null;occurredAt:string;action:string;entityType:string;entityId:string;
  previousValuesJson?:string|null;newValuesJson?:string|null;origin:string;ipAddress?:string|null;correlationId?:string|null;
}
export interface AuditPage {items:AuditLogItem[];total:number;page:number;pageSize:number;}
