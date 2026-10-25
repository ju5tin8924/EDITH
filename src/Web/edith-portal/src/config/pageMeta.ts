import { useLocation } from 'react-router-dom';

export interface PageMeta {
  title: string;
  parent?: string;
  definition: string;
}

export const ROUTE_PAGE_META: Record<string, PageMeta> = {
  // 1. Home / Dashboard
  '/dashboard': {
    title: 'Home / Dashboard',
    definition: 'Real-time operational telemetry, screening throughput, SLA compliance, and watchlist health across all corridors.'
  },

  // 2. Blacklist & Screening
  '/name-search': {
    title: 'Name Search',
    parent: 'Blacklist',
    definition: 'Screen individuals, corporate entities, vessels, and identifiers against global sanctions, PEP, and adverse media lists.'
  },
  '/screening': {
    title: 'Sanction Screening',
    parent: 'Blacklist',
    definition: 'Evaluate real-time wire transfers and counterparties against OFAC, UN, EU, UK HMT, and CBUAE sanctions registries.'
  },
  '/blacklist/screening': {
    title: 'Sanction Screening',
    parent: 'Blacklist',
    definition: 'Evaluate real-time wire transfers and counterparties against OFAC, UN, EU, UK HMT, and CBUAE sanctions registries.'
  },
  '/blacklist/watchlist': {
    title: 'Add/Edit Watch list',
    parent: 'Blacklist',
    definition: 'Manage internal watchlist entries, false-positive suppression rules, and time-bound entity exceptions.'
  },
  '/blacklist/bulk': {
    title: 'Bulk Screening',
    parent: 'Blacklist',
    definition: 'Upload and screen high-volume CSV or ISO-20022 batch files asynchronously against consolidated watchlists.'
  },
  '/bulk': {
    title: 'Bulk Screening',
    parent: 'Blacklist',
    definition: 'Upload and screen high-volume CSV or ISO-20022 batch files asynchronously against consolidated watchlists.'
  },

  // 3. White List
  '/whitelist': {
    title: 'White List',
    definition: 'Govern approved false-positive suppressions and time-bound entity clearances with dual-control authorization.'
  },

  // 4. AML
  '/rules': {
    title: 'Customize Rules',
    parent: 'AML',
    definition: 'Configure transaction monitoring scenarios, velocity thresholds, and FATF corridor risk parameters.'
  },
  '/aml/rules': {
    title: 'Customize Rules',
    parent: 'AML',
    definition: 'Configure transaction monitoring scenarios, velocity thresholds, and FATF corridor risk parameters.'
  },
  '/sandbox': {
    title: 'Process AML',
    parent: 'AML',
    definition: 'Simulate scoring weightages and backtest AML rule thresholds against historical transaction traffic.'
  },
  '/aml/process': {
    title: 'Process AML',
    parent: 'AML',
    definition: 'Simulate scoring weightages and backtest AML rule thresholds against historical transaction traffic.'
  },
  '/aml/str-history': {
    title: 'STR History',
    parent: 'AML',
    definition: 'Review historical Suspicious Transaction Reports, adjudication decisions, and archived investigation files.'
  },
  '/aml/mlro-monitoring': {
    title: 'ISTR MLRO Monitoring',
    parent: 'AML',
    definition: 'Supervise escalated AML cases, statutory freeze orders, and pending MLRO final determinations.'
  },

  // 5. STR Process
  '/cases': {
    title: 'Alerts',
    parent: 'STR Process',
    definition: 'Investigate flagged transactions, review phonetic match breakdowns, and adjudicate compliance alerts.'
  },
  '/str-process/alerts': {
    title: 'Alerts',
    parent: 'STR Process',
    definition: 'Triage incoming real-time screening holds and suspicious activity alerts requiring L1 and L2 investigation.'
  },
  '/str-process/edd-analyst': {
    title: 'EDD ANALYST & STR Branch [KYC]',
    parent: 'STR Process',
    definition: 'Conduct Enhanced Due Diligence, UBO cascade screening, and source-of-wealth verification for high-risk clients.'
  },
  '/str-process/istr-mlro': {
    title: 'ISTR MLRO (Freeze/Unfreeze)',
    parent: 'STR Process',
    definition: 'Authorize statutory account freezes, asset unfreezes, and regulatory STR escalations under 4-eyes control.'
  },
  '/str-process/edd-monitoring': {
    title: 'EDD Analyst Monitoring',
    parent: 'STR Process',
    definition: 'Monitor ongoing Enhanced Due Diligence investigations, SLA countdowns, and analyst case queues.'
  },

  // 6. Reports
  '/reports': {
    title: 'Reports',
    definition: 'Generate regulatory compliance reports, corridor risk summaries, and cryptographically signed audit exports.'
  },
  '/reports/alerts': {
    title: 'Alert Reports',
    parent: 'Reports',
    definition: 'Analyze screening alert volumes, hit-to-clear ratios, and disposition trends across business units.'
  },
  '/reports/transactions': {
    title: 'Transaction Report',
    parent: 'Reports',
    definition: 'Audit screened cross-border and domestic payment flows by corridor, value tier, and settlement status.'
  },
  '/reports/blacklist': {
    title: 'Blacklist Report (WC/PEP)',
    parent: 'Reports',
    definition: 'Review matched entities across World-Check PEP, OFAC SDN, UN Consolidated, and local terrorist designations.'
  },
  '/reports/releases': {
    title: 'Transaction Release Report',
    parent: 'Reports',
    definition: 'Track held transactions released after compliance clearance, including officer rationale and turnaround times.'
  },
  '/reports/online-str': {
    title: 'Online STR Report',
    parent: 'Reports',
    definition: 'Inspect submitted Suspicious Transaction Reports, FIU acknowledgment receipts, and filing timelines.'
  },
  '/reports/analytics': {
    title: 'Transaction Analytics Report',
    parent: 'Reports',
    definition: 'Evaluate corridor risk concentration, SLA adherence metrics, and false-positive reduction performance.'
  },

  // 7. Log Report
  '/notifications': {
    title: 'Log Report',
    definition: 'Configure real-time compliance alert routing and inspect system process, upload, and dispatch logs.'
  },
  '/log-report': {
    title: 'Log Report',
    definition: 'Inspect operational execution logs, batch upload records, and real-time alert dispatch trails.'
  },
  '/log-report/process': {
    title: 'Process Log',
    parent: 'Log Report',
    definition: 'Monitor real-time screening engine execution trails, rule evaluation logs, and webhook delivery status.'
  },
  '/log-report/upload': {
    title: 'Upload Log',
    parent: 'Log Report',
    definition: 'Audit watchlist feed ingestions, bulk CSV batch uploads, and schema validation results.'
  },
  '/log-report/suspicious': {
    title: 'Suspicious Log',
    parent: 'Log Report',
    definition: 'Review high-risk match triggers, statutory freeze events, and emergency escalation dispatches.'
  },
  '/log-report/custom': {
    title: 'Custom Reports',
    parent: 'Log Report',
    definition: 'Configure and preview custom notification dispatch templates and filtered operational log views.'
  },
  '/log-report/monthly-screen': {
    title: 'Monthly Screen Data Report',
    parent: 'Log Report',
    definition: 'Review monthly aggregated screening volumes, watchlist delta summaries, and SLA delivery logs.'
  },

  // 8. Settings
  '/settings': {
    title: 'Settings',
    definition: 'Configure platform risk benchmarks, screening score weights, and administrative access controls.'
  },
  '/settings/user-groups': {
    title: 'User Groups',
    parent: 'Settings',
    definition: 'Define role-based compliance groups and assign view, edit, and authorization privileges.'
  },
  '/settings/users': {
    title: 'Users',
    parent: 'Settings',
    definition: 'Provision compliance officers, manage 2FA enrollment, and control shift duty status.'
  },
  '/settings/menu-master': {
    title: 'Menu Master',
    parent: 'Settings',
    definition: 'Inspect and manage the platform navigation hierarchy, route paths, and module codes.'
  },
  '/settings/menu-mappings': {
    title: 'Menu Mappings',
    parent: 'Settings',
    definition: 'Configure granular menu visibility and action entitlements across compliance user groups.'
  },
  '/settings/risk-rates': {
    title: 'Risk Rate Settings',
    parent: 'Settings',
    definition: 'Calibrate high- and medium-risk score thresholds for customer onboarding and payment screening.'
  },
  '/settings/screening-score': {
    title: 'Screening Score',
    parent: 'Settings',
    definition: 'Adjust phonetic name, nationality, and date-of-birth weightages for the 5-pass matching engine.'
  },
  '/settings/setup-control': {
    title: 'Setup Control',
    parent: 'Settings',
    definition: 'Enable or disable real-time screening interceptors across customer, beneficiary, and UBO workflows.'
  },
  '/settings/change-password': {
    title: 'Change Password',
    parent: 'Settings',
    definition: 'Rotate your operator account password in compliance with enterprise credential security policies.'
  },
  '/settings/risk-assessment': {
    title: 'Risk Assessment Master',
    parent: 'Settings',
    definition: 'Configure composite KYC risk weighting across geographic, client-type, and product dimensions.'
  },
  '/iam': {
    title: 'Users',
    parent: 'Settings',
    definition: 'Manage operator accounts, role-based access groups, and navigation menu entitlements.'
  },

  // 9. Master
  '/master': {
    title: 'Master',
    definition: 'Maintain reference data tables for FATF country risk tiers, nationalities, and customer blocklists.'
  },
  '/master/nationality': {
    title: 'Nationality Master',
    parent: 'Master',
    definition: 'Manage nationality demonyms, ISO codes, and associated CDD/EDD risk classifications.'
  },
  '/master/country': {
    title: 'Country Master',
    parent: 'Master',
    definition: 'Maintain sovereign country risk scores and FATF Black and Grey list designations.'
  },
  '/master/unblock-customer': {
    title: 'Unblock Customer',
    parent: 'Master',
    definition: 'Review and authorize administrative unblocking of restricted customer accounts with audit logging.'
  },

  // 10. Authorization
  '/goaml': {
    title: 'Authorization',
    definition: 'Prepare, validate, and dispatch statutory STR, SAR, and AIF XML filings to the Financial Intelligence Unit.'
  },
  '/authorization': {
    title: 'Authorization',
    definition: 'Review and authorize pending sanction alerts, customer risk recalculations, and regulatory filings.'
  },
  '/authorization/sanction-alerts': {
    title: 'Sanction Alert Authorization',
    parent: 'Authorization',
    definition: 'Perform 4-eyes supervisory sign-off on held sanction alerts, transaction releases, and blocks.'
  },
  '/authorization/risk-calculate': {
    title: 'Risk Calculate',
    parent: 'Authorization',
    definition: 'Authorize customer risk tier recalculations and validate regulatory reporting payloads.'
  },
  '/authorization/customer': {
    title: 'Customer Authorization',
    parent: 'Authorization',
    definition: 'Approve high-risk customer onboarding determinations and statutory regulatory submissions.'
  },

  // 11. Customer
  '/kyc360': {
    title: 'Customer KYC',
    parent: 'Customer',
    definition: 'Review holistic customer CDD/EDD profiles, UBO hierarchies, behavioral drift, and verified KYC documents.'
  },
  '/customer/kyc': {
    title: 'Customer KYC',
    parent: 'Customer',
    definition: 'Review holistic customer CDD/EDD profiles, UBO hierarchies, behavioral drift, and verified KYC documents.'
  },
  '/customer/screening-report': {
    title: 'Customer Screening Report',
    parent: 'Customer',
    definition: 'Run and export certified screening evaluations and WORM audit certificates for customer entities.'
  },

  // 12. Delta
  '/delta': {
    title: 'Delta',
    definition: 'Rescreen existing customer portfolios against newly published nightly watchlist additions and modifications.'
  },
  '/delta/authorization': {
    title: 'Authorization',
    parent: 'Delta',
    definition: 'Review and authorize batch dispositions for newly identified nightly watchlist delta matches.'
  },
  '/delta/log-report': {
    title: 'Log Report',
    parent: 'Delta',
    definition: 'Audit nightly watchlist synchronization batches, delta scan coverage, and match resolution logs.'
  }
};

export function usePageMeta(fallbackTitle: string, fallbackDefinition: string): PageMeta {
  const { pathname } = useLocation();
  return (
    ROUTE_PAGE_META[pathname] || {
      title: fallbackTitle,
      definition: fallbackDefinition
    }
  );
}
