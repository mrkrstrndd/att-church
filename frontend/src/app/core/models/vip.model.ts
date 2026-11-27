export interface VipReportItem {
  id: string;
  fullName: string;
  email: string;
  phoneNo: string | null;
  invitedByName: string | null;
  signInCount: number;
  joinedAt: string;
}

export interface VipReport {
  generatedOn: string;
  totalVips: number;
  totalSignIns: number;
  members: VipReportItem[];
}
