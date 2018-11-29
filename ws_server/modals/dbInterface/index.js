const config = require('../../config');
const pg = require('pg');

class DbInterface {
	constructor() {
		this.connectionString = "postgres://"+
		config.DB_USER_NAME+":"+
		config.DB_USER_PASSWORD+"@"+
		config.DB_DOMAIN+":"+
		config.DB_PORT+"/"+config.DB_NAME;

		this.pool = new pg.Pool({
			connectionString: this.connectionString
		});
	}

	sqlQuery(query, callback) {
		this.pool.query(query, (err, res) => {
			callback(this.constructor.convertResponseToOutput(res))
		});
	}

	disconnect() {
		this.pool.end();
	}

	static convertResponseToOutput(result) {
		if (result.rowCount > 0)
			return result.rows
		return []
	}
}

module.exports = DbInterface;
