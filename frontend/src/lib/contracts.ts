export interface InvoiceLine {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  confidence: number | null;
}

export interface InvoiceReviewHistory {
  action: string;
  detailsJson: string;
  occurredAtUtc: string;
  performedByUserId: string;
}

export interface Invoice {
  id: string;
  supplierId: string | null;
  invoiceNumber: string | null;
  issueDate: string | null;
  currencyCode: string | null;
  subtotal: number | null;
  taxAmount: number | null;
  total: number | null;
  status: string;
  uploadedAtUtc: string;
  lines: InvoiceLine[];
  reviewHistory: InvoiceReviewHistory[];
  supplierName: string | null;
  supplierTaxId: string | null;
  customerName: string | null;
  customerTaxId: string | null;
  dueDate: string | null;
  purchaseOrderNumber: string | null;
  extractionStatus: string | null;
  extractionAttemptCount: number;
  lastExtractionErrorCode: string | null;
  extractionCompletedAtUtc: string | null;
  fieldConfidence: Record<string, number | null> | null;
  validationWarnings: string[] | null;
  duplicateOfInvoiceId: string | null;
}

export interface InvoicePage {
  items: Invoice[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface InvoiceUploadResult {
  invoice: Invoice;
  extractionSucceeded: boolean;
  extractionErrorCode: string | null;
  extractionErrorMessage: string | null;
}

export interface InvoiceReviewRequest {
  invoiceNumber: string | null;
  issueDate: string | null;
  currencyCode: string | null;
  subtotal: number | null;
  taxAmount: number | null;
  total: number | null;
  reason: string | null;
}

export interface ChatSource {
  title: string;
  uri: string | null;
}

export interface ChatResponse {
  reply: string;
  sources: ChatSource[];
}

export interface InvoiceExplanation {
  invoiceId: string;
  explanation: string;
  warnings: string[];
  duplicateMatches: DuplicateMatch[];
}

export interface DuplicateMatch {
  invoiceId: string;
  invoiceNumber: string | null;
  invoiceDate: string | null;
  total: number | null;
  currencyCode: string | null;
  matchType: string;
  matchedRules: string[];
  score: number | null;
}

export interface ManagementReport {
  fromUtc: string;
  toUtc: string;
  processedInvoiceCount: number;
  duplicateInvoiceCount: number;
  duplicateRate: number;
  approvedTotalsByCurrency: Array<{
    currencyCode: string;
    totalAmount: number;
  }>;
  criticalInventoryItemCount: number;
}

export interface AgentHealth {
  status: "Healthy" | "Unhealthy" | "NotConfigured" | string;
}

export interface AgentsHealth {
  businessAgent: AgentHealth;
  invoiceAgent: AgentHealth;
  modelDeployment: string | null;
  checkedAtUtc: string;
}
