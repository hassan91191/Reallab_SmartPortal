const { getClients } = require('./_google');

const REG_TAB_DEFAULT = 'Labs';

function normalize(s) {
  return String(s || '').trim();
}

function normalizeKey(s) {
  return normalize(s).toUpperCase();
}

function looksLikeGoogleId(id) {
  id = normalize(id);
  if (!id) return false;
  // IDs عادة طويلة ومفيهاش مسافات
  if (id.length < 15) return false;
  if (/\s/.test(id)) return false;
  return true;
}

function asHeaderMap(headerRow) {
  const map = {};
  headerRow.forEach((h, idx) => {
    const key = normalize(h).toLowerCase();
    if (!key) return;
    map[key] = idx;
  });
  return map;
}

function pick(row, headerMap, ...keysOrIndexes) {
  for (const k of keysOrIndexes) {
    if (typeof k === 'number') {
      if (row[k] != null && normalize(row[k])) return normalize(row[k]);
    } else {
      const idx = headerMap[k.toLowerCase()];
      if (idx != null && row[idx] != null && normalize(row[idx])) return normalize(row[idx]);
    }
  }
  return '';
}

async function getLabConfig(labKey) {
  const registrySheetId = process.env.REGISTRY_SHEET_ID;
  const tab = process.env.REGISTRY_SHEET_TAB || REG_TAB_DEFAULT;
  if (!registrySheetId) throw new Error('Missing env REGISTRY_SHEET_ID');

  const labKeyNorm = normalizeKey(labKey);
  if (!labKeyNorm) throw new Error('Missing lab');

  const { sheets } = getClients();
  const range = `${tab}!A1:F5000`;
  const resp = await sheets.spreadsheets.values.get({
    spreadsheetId: registrySheetId,
    range,
    majorDimension: 'ROWS',
  });

  const rows = resp.data.values || [];
  if (rows.length === 0) throw new Error('Registry sheet is empty');

  const headerMap = asHeaderMap(rows[0] || []);

  let found = null;
  for (let i = 1; i < rows.length; i++) {
    const r = rows[i] || [];
    const key = normalizeKey(pick(r, headerMap, 'labkey', 0));

    if (key && key === labKeyNorm) {
      const driveFolderId = pick(r, headerMap, 'drivefolderid', 'drivefolder', 1);
      const logSheetId = pick(r, headerMap, 'logsheetid', 'logsheet', 2);

      found = {
        rowIndex1Based: i + 1,
        labKey: key,
        driveFolderId,
        logSheetId,
        logoFileId: pick(r, headerMap, 'logofileid', 'logo', 3),
        title: pick(r, headerMap, 'title', 4),
        subtitle: pick(r, headerMap, 'subtitle', 5),
      };
      break;
    }
  }

  if (!found) {
    const err = new Error('Lab not registered');
    err.code = 'LAB_NOT_REGISTERED';
    throw err;
  }

  // ✅ Validation: لازم IDs حقيقية
  if (!looksLikeGoogleId(found.driveFolderId) || !looksLikeGoogleId(found.logSheetId)) {
    const err = new Error('Registry values are not IDs (DriveFolderId/LogSheetId look invalid).');
    err.code = 'LAB_CONFIG_INVALID_IDS';
    err.details = { driveFolderId: found.driveFolderId, logSheetId: found.logSheetId };
    throw err;
  }

  return found;
}

async function upsertLabConfig(payload) {
  const registrySheetId = process.env.REGISTRY_SHEET_ID;
  const tab = process.env.REGISTRY_SHEET_TAB || REG_TAB_DEFAULT;
  if (!registrySheetId) throw new Error('Missing env REGISTRY_SHEET_ID');

  const labKey = normalizeKey(payload.labKey);
  if (!labKey) throw new Error('Missing labKey');

  const driveFolderId = normalize(payload.driveFolderId);
  const logSheetId = normalize(payload.logSheetId);
  const logoFileId = normalize(payload.logoFileId);
  const title = normalize(payload.title);
  const subtitle = normalize(payload.subtitle);

  if (!looksLikeGoogleId(driveFolderId) || !looksLikeGoogleId(logSheetId)) {
    throw new Error('driveFolderId and logSheetId must be valid Google IDs (no spaces, long enough).');
  }

  const { sheets } = getClients();
  const rangeAll = `${tab}!A1:F5000`;

  const resp = await sheets.spreadsheets.values.get({
    spreadsheetId: registrySheetId,
    range: rangeAll,
    majorDimension: 'ROWS',
  });

  const rows = resp.data.values || [];
  if (rows.length === 0) {
    await sheets.spreadsheets.values.update({
      spreadsheetId: registrySheetId,
      range: `${tab}!A1:F1`,
      valueInputOption: 'RAW',
      requestBody: { values: [[ 'LabKey', 'DriveFolderId', 'LogSheetId', 'LogoFileId', 'Title', 'Subtitle' ]] },
    });
  }

  const resp2 = await sheets.spreadsheets.values.get({
    spreadsheetId: registrySheetId,
    range: rangeAll,
    majorDimension: 'ROWS',
  });

  const rows2 = resp2.data.values || [];
  const headerMap = asHeaderMap(rows2[0] || []);

  let existingRow = -1;
  for (let i = 1; i < rows2.length; i++) {
    const key = normalizeKey(pick(rows2[i] || [], headerMap, 'labkey', 0));
    if (key === labKey) { existingRow = i + 1; break; }
  }

  const values = [[ labKey, driveFolderId, logSheetId, logoFileId, title, subtitle ]];

  if (existingRow > 0) {
    await sheets.spreadsheets.values.update({
      spreadsheetId: registrySheetId,
      range: `${tab}!A${existingRow}:F${existingRow}`,
      valueInputOption: 'RAW',
      requestBody: { values },
    });
    return { action: 'updated', row: existingRow };
  }

  await sheets.spreadsheets.values.append({
    spreadsheetId: registrySheetId,
    range: `${tab}!A:F`,
    valueInputOption: 'RAW',
    insertDataOption: 'INSERT_ROWS',
    requestBody: { values },
  });
  return { action: 'inserted' };
}

module.exports = { getLabConfig, upsertLabConfig };
