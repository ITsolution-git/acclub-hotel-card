const request = require('request');

module.exports = class ApiInterface {
	constructor(apiEndpoint, apiKey, ip = 1, userAgent = 2) {
		this.endpoint = apiEndpoint;
		this.ip = ip;
		this.userAgent = userAgent;
		this.apiKey = apiKey;
	}

	call(method, data, callback) {
		data = Object.assign({}, {
			apiKey: this.apiKey,
			method: method,
			userAgent: this.userAgent,
			userIp: this.ip
		}, data);

		request.post({
			encoding: 'utf-8',
			method: 'POST',
			rejectUnauthorized: false,
			uri: process.env.API_DOMAIN + this.endpoint,
			headers: {
				'User-Agent': 'Hivepbx'
			},
			body: JSON.stringify(data),
		}, callback);
	}

	static isResultOK(responseObject) {
		if(typeof responseObject !== 'object') {
			console.warn('Provided response from API was not a valid Object!');
			return false;
		}

		if(!responseObject.hasOwnProperty('RESULT')) {
			console.warn('Supplied object scheme was invalid! Please make sure the provided Object is a valid API response!');
			return false;
		}

		return (responseObject.RESULT === 'OK');
	}
}
