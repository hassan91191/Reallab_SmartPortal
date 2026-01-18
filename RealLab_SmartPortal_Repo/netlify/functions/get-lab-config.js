const { getLabConfig } = require('./_registry');

exports.handler = async (event) => {
  try {
    const lab = (event.queryStringParameters?.lab || '').trim();
    if (!lab) {
      return json(400, { error: 'missing_lab', message: 'Missing lab query parameter' });
    }

    const cfg = await getLabConfig(lab);
    if (!cfg) {
      return json(404, { error: 'lab_not_found', message: 'LabKey not found in Registry' });
    }

    // Don’t leak sheet IDs unnecessarily to client, but it’s ok if you want to display/debug.
    // We return them because the desktop app and debugging might need them.
    return json(200, {
      labKey: cfg.labKey,
      driveFolderId: cfg.driveFolderId,
      logSheetId: cfg.logSheetId,
      logoFileId: cfg.logoFileId,
      title: cfg.title || 'بوابة النتائج الذكية',
      subtitle: cfg.subtitle || 'نتائج التحاليل الطبية',
    });
  } catch (e) {
    return json(500, { error: 'server_error', message: e.message || String(e) });
  }
};

function json(statusCode, body) {
  return {
    statusCode,
    headers: {
      'content-type': 'application/json; charset=utf-8',
      'cache-control': 'no-store',
    },
    body: JSON.stringify(body),
  };
}
