const { getClients } = require('./_google');
const { getLabConfig } = require('./_registry');

const DEFAULT_TAB = 'Patient Lab Log';

exports.handler = async (event) => {
  try {
    if (event.httpMethod !== 'POST') return json(405, { error: 'method_not_allowed' });

    const payload = safeJson(event.body);
    const lab = (payload.lab || event.queryStringParameters?.lab || '').trim();
    const patientId = (payload.patientId || payload.id || '').trim();
    const fileName = (payload.fileName || '').toString();
    const fileId = (payload.fileId || '').toString();
    const action = (payload.action || 'VIEW').toString();
    const userAgent = (payload.userAgent || event.headers['user-agent'] || '').toString();
    const tabName = (process.env.LAB_LOG_TAB_NAME || DEFAULT_TAB).trim();

    if (!lab) return json(400, { error: 'missing_lab' });
    if (!patientId) return json(400, { error: 'missing_patientId' });

    const cfg = await getLabConfig(lab);
    if (!cfg) return json(404, { error: 'lab_not_found' });
    if (!cfg.logSheetId) return json(500, { error: 'lab_not_ready', message: 'LogSheetId not configured for this lab' });

    const { sheets } = getClients();

    const row = [
      new Date().toISOString(),
      patientId,
      fileName,
      fileId,
      action,
      userAgent
    ];

    await appendWithAutoInit(sheets, cfg.logSheetId, tabName, row);
    return json(200, { ok: true });
  } catch (e) {
    return json(500, { error: 'server_error', message: e.message || String(e) });
  }
};

async function appendWithAutoInit(sheets, spreadsheetId, tabName, row) {
  try {
    await sheets.spreadsheets.values.append({
      spreadsheetId,
      range: `${tabName}!A:F`,
      valueInputOption: 'RAW',
      insertDataOption: 'INSERT_ROWS',
      requestBody: { values: [row] },
    });
    return;
  } catch (e) {
    // If tab missing, create it + add headers then retry once
    const msg = (e?.errors?.[0]?.message || e.message || '').toLowerCase();
    if (msg.includes('unable to parse range') || msg.includes('not found') || msg.includes('invalid')) {
      await ensureTabAndHeader(sheets, spreadsheetId, tabName);
      await sheets.spreadsheets.values.append({
        spreadsheetId,
        range: `${tabName}!A:F`,
        valueInputOption: 'RAW',
        insertDataOption: 'INSERT_ROWS',
        requestBody: { values: [row] },
      });
      return;
    }
    throw e;
  }
}

async function ensureTabAndHeader(sheets, spreadsheetId, tabName) {
  const meta = await sheets.spreadsheets.get({ spreadsheetId });
  const sheetsList = meta.data.sheets || [];
  const exists = sheetsList.some(s => (s.properties?.title || '') === tabName);
  if (!exists) {
    await sheets.spreadsheets.batchUpdate({
      spreadsheetId,
      requestBody: {
        requests: [
          { addSheet: { properties: { title: tabName } } }
        ]
      }
    });
  }

  // Header row (only if empty)
  const valuesRes = await sheets.spreadsheets.values.get({
    spreadsheetId,
    range: `${tabName}!A1:F1`
  });
  const firstRow = valuesRes.data.values?.[0] || [];
  if (firstRow.length === 0) {
    await sheets.spreadsheets.values.update({
      spreadsheetId,
      range: `${tabName}!A1:F1`,
      valueInputOption: 'RAW',
      requestBody: {
        values: [[
          'DateTime',
          'PatientID',
          'FileName',
          'FileId',
          'Action',
          'UserAgent'
        ]]
      }
    });
  }
}

function safeJson(txt) {
  try { return txt ? JSON.parse(txt) : {}; } catch { return {}; }
}

function json(statusCode, body) {
  return {
    statusCode,
    headers: {
      'content-type': 'application/json; charset=utf-8',
      'cache-control': 'no-store'
    },
    body: JSON.stringify(body)
  };
}
