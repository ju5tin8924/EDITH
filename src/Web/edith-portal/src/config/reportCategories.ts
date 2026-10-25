import { MenuItemEntitlement } from '../types';

export type ReportTabId =
  | 'WORM_AUDIT'
  | 'ALERTS'
  | 'TRANSACTIONS'
  | 'BLACKLIST'
  | 'RELEASES'
  | 'ONLINE_STR'
  | 'ANALYTICS'
  | 'LOG_PROCESS'
  | 'LOG_UPLOAD'
  | 'LOG_SUSPICIOUS'
  | 'LOG_CUSTOM'
  | 'LOG_MONTHLY'
  | 'STR_HISTORY'
  | 'CUSTOMER_SCREENING'
  | 'DELTA_LOG';

export interface ReportCategoryItem {
  id: ReportTabId;
  code: string;
  label: string;
  path: string;
  icon: string;
}

export interface ReportCategoryGroup {
  group: string;
  items: ReportCategoryItem[];
}

export const REPORT_CATEGORIES: ReportCategoryGroup[] = [
  {
    group: 'WORM Ledger',
    items: [
      {
        id: 'WORM_AUDIT',
        code: 'MENU_REPORTS_WORM',
        label: 'WORM Audit Ledger',
        path: '/reports',
        icon: 'ShieldCheck'
      }
    ]
  },
  {
    group: 'Regulatory Reports',
    items: [
      {
        id: 'ALERTS',
        code: 'MENU_REPORTS_ALERTS',
        label: 'Alert Reports',
        path: '/reports/alerts',
        icon: 'AlertTriangle'
      },
      {
        id: 'TRANSACTIONS',
        code: 'MENU_REPORTS_TRANSACTIONS',
        label: 'Transaction Report',
        path: '/reports/transactions',
        icon: 'FileSpreadsheet'
      },
      {
        id: 'BLACKLIST',
        code: 'MENU_REPORTS_BLACKLIST',
        label: 'Blacklist Report (WC/PEP)',
        path: '/reports/blacklist',
        icon: 'ShieldAlert'
      },
      {
        id: 'RELEASES',
        code: 'MENU_REPORTS_RELEASES',
        label: 'Transaction Release Report',
        path: '/reports/releases',
        icon: 'CheckCircle2'
      },
      {
        id: 'ONLINE_STR',
        code: 'MENU_REPORTS_ONLINE_STR',
        label: 'Online STR Report',
        path: '/reports/online-str',
        icon: 'FileText'
      },
      {
        id: 'ANALYTICS',
        code: 'MENU_REPORTS_ANALYTICS',
        label: 'Transaction Analytics Report',
        path: '/reports/analytics',
        icon: 'BarChart3'
      }
    ]
  },
  {
    group: 'Operational Logs',
    items: [
      {
        id: 'LOG_PROCESS',
        code: 'MENU_LOG_PROCESS',
        label: 'Process Log',
        path: '/reports/process-log',
        icon: 'FileText'
      },
      {
        id: 'LOG_UPLOAD',
        code: 'MENU_LOG_UPLOAD',
        label: 'Upload Log',
        path: '/reports/upload-log',
        icon: 'Layers'
      },
      {
        id: 'LOG_SUSPICIOUS',
        code: 'MENU_LOG_SUSPICIOUS',
        label: 'Suspicious Log',
        path: '/reports/suspicious-log',
        icon: 'ShieldAlert'
      },
      {
        id: 'LOG_CUSTOM',
        code: 'MENU_LOG_CUSTOM',
        label: 'Custom Reports',
        path: '/reports/custom-reports',
        icon: 'FileSpreadsheet'
      },
      {
        id: 'LOG_MONTHLY',
        code: 'MENU_LOG_MONTHLY_SCREEN',
        label: 'Monthly Screen Data',
        path: '/reports/monthly-screen',
        icon: 'FileText'
      }
    ]
  },
  {
    group: 'Specialized Logs',
    items: [
      {
        id: 'STR_HISTORY',
        code: 'MENU_AML_STR_HISTORY_REP',
        label: 'STR History',
        path: '/reports/str-history',
        icon: 'FileText'
      },
      {
        id: 'CUSTOMER_SCREENING',
        code: 'MENU_CUSTOMER_SCREENING_REP',
        label: 'Customer Screening Report',
        path: '/reports/customer-screening',
        icon: 'Search'
      },
      {
        id: 'DELTA_LOG',
        code: 'MENU_DELTA_LOG_REP',
        label: 'Delta Log Report',
        path: '/reports/delta-log',
        icon: 'RefreshCw'
      }
    ]
  }
];

// Single source of truth transformed for Sidebar menu items
export const REPORT_SUB_MENUS: MenuItemEntitlement[] = REPORT_CATEGORIES.flatMap(category =>
  category.items.map(item => ({
    code: item.code,
    label: item.label,
    path: item.path,
    icon: item.icon,
    enabled: true,
    subGroup: category.group
  }))
);

export function resolveTabFromPath(pathname: string): ReportTabId {
  if (pathname.includes('/alerts')) return 'ALERTS';
  if (pathname.includes('/transactions')) return 'TRANSACTIONS';
  if (pathname.includes('/blacklist')) return 'BLACKLIST';
  if (pathname.includes('/releases')) return 'RELEASES';
  if (pathname.includes('/online-str')) return 'ONLINE_STR';
  if (pathname.includes('/analytics')) return 'ANALYTICS';
  if (pathname.includes('/process-log') || pathname.includes('/log-report/process') || pathname.includes('/log-process')) return 'LOG_PROCESS';
  if (pathname.includes('/upload-log') || pathname.includes('/log-report/upload') || pathname.includes('/log-upload')) return 'LOG_UPLOAD';
  if (pathname.includes('/suspicious-log') || pathname.includes('/log-report/suspicious') || pathname.includes('/log-suspicious')) return 'LOG_SUSPICIOUS';
  if (pathname.includes('/custom-reports') || pathname.includes('/log-report/custom') || pathname.includes('/log-custom')) return 'LOG_CUSTOM';
  if (pathname.includes('/monthly-screen') || pathname.includes('/log-report/monthly-screen') || pathname.includes('/log-monthly')) return 'LOG_MONTHLY';
  if (pathname.includes('str-history')) return 'STR_HISTORY';
  if (pathname.includes('screening-report') || pathname.includes('customer-screening')) return 'CUSTOMER_SCREENING';
  if (pathname.includes('delta')) return 'DELTA_LOG';
  return 'WORM_AUDIT';
}
