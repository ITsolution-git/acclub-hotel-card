require('dotenv').load();

const config = require('./config'),
	ApiInterface = require('./apiInterface');

// Modals
const DbInterface = require('./modals/dbInterface');

//require('ssl-root-cas').inject(); @TODO: When we get ready for production.

const server = require('http').createServer(),
	io = require('socket.io')(server);

class HotelCardSocketServer {

	constructor() {
		// PLEASE PAY ATTENTION TO THIS VALUE IN CONSOLE LOGS
		// THIS WILL AVOID UNWARANTED GOOSE CHASING.

		this.SOFTWARE_VERSION = '0.1.1';

		// Server Session Data.
		this.appSession = {
			uid: '',
			sessionToken: ''
		}

		this.port = config.SERVER_PORT;

		this.dbconnection = new DbInterface();
                this.dbconnection.sqlQuery("select * from hotel_room", (res)=>{console.log(res)});
	}

	run() {
		console.log(`Starting server ver ${this.SOFTWARE_VERSION}`);

		this.setupWebsocket();
	}

	setupWebsocket() {
		io.on('connection', (client) => this.socketioOnConnection(client, this.dbconnection));

		server.listen(this.port, '0.0.0.0');
		console.log(`Server running! ${this.port}`);
	}

	socketioOnConnection(client, dbconn) {
		// process.stdout.write('\x1Bc');
		// or
		// console.log('\x1Bc');

		console.log("connect");

		//For Tracking When User Disconnects:
		io.sockets.on("disconnect",function(socket){
			//var socket is the socket for the client who has disconnected.
			console.log("disconnected");
		});

		client.on("issue_card", function(data){
			if (data['product_id'] == "undefined" || data['product_id'][0] == "undefined") return;
			var sql = "select cards from hotel_room where product_id='" + 
			data['product_id'][0] + "'";

			dbconn.sqlQuery(sql, (res)=>{
				if (res.length != 0 && res[0] != "undefined") {
					data['prev_cardno'] = res[0]["cards"]
					io.sockets.emit("issue_card", data);
				}
			});
		});

		client.on("delete_card", function(data){
			io.sockets.emit("delete_card", data);
		});

		client.on("delete_card_hr", function(data){
			data['issue'] = "0";
			data['card_id'] = data['id'];
			if (data['user_id'] == false)
                                io.sockets.emit("delete_card_hr", data);
                        else {
                                console.log(data['user_id']);
                                var sql = "select partner_id from res_users where id='" + data['user_id'][0].toString() + "'";
                                dbconn.sqlQuery(sql, (res)=>{
                                        if (res.length !=0 && res[0] !== 'undefined') {
                                                data['card_id'] = res[0]['partner_id'];
                                                io.sockets.emit("delete_card_hr", data);
                                        }
                                });
                        }
		});
		client.on("issue_card_hr", function(data){
			data['issue'] = "1";
			data['card_id'] = data['id'];
			if (data['user_id'] == false)
				io.sockets.emit("delete_card_hr", data);
			else {
				console.log(data['user_id']);
				var sql = "select partner_id from res_users where id='" + data['user_id'][0].toString() + "'";
				dbconn.sqlQuery(sql, (res)=>{
                                        if (res.length !=0 && res[0] !== 'undefined') {
						data['card_id'] = res[0]['partner_id'];
						io.sockets.emit("delete_card_hr", data);
					}
				});
			}
		});
		client.on("delete_card_hr_confirm", function(data){
			data = JSON.parse(data);
			console.log(data);
			var sql = "update res_partner set card_no = '' where id = '" +
				data['partner'] + "'";
			if (data['issue'] == "1")
				sql = "update res_partner set card_no = '"+data['cardno']+"' where id = '" +
					data['partner'] + "'";

			dbconn.sqlQuery(sql, (res)=>{
			});
		});

		client.on("write_cardno", function(data){
			data = JSON.parse(data);
			var sql = "update hotel_room set cards = '" +
				data['cardno'] + "' where product_id = '" +
				data['product_no'] + "'";

			dbconn.sqlQuery(sql, (res)=>{
			});
		});

		client.on("get_url", function(data){
			var result_data = {};
			data = JSON.parse(data);

			if (data["hotel"] == "true"){
				var sql = "select id, card_no, customer from res_partner where card_no='" + data['cardno'] + "';";
				// console.log(sql);
				dbconn.sqlQuery(sql, (res)=>{
					if (res.length !=0 && res[0] !== 'undefined') {
						console.log(res)
						if(res[0]['customer'] == true) {
							result_data['type'] = 'customer';
							result_data['customer_id'] = res[0]["id"];
							io.sockets.emit("get_url", result_data);
						}
						else {
							result_data['type'] = 'partner';
							var sql = "select hr_employee.id as partner_id from res_partner join res_users on res_partner.id = res_users.partner_id \
									join resource_resource on res_users.id = resource_resource.user_id \
									join hr_employee on resource_resource.id = hr_employee.resource_id \
									where res_partner.id='" + res[0]['id'].toString() + "';";
                               				dbconn.sqlQuery(sql, (res)=>{
                        			                if (res.length !=0 && res[0] !== 'undefined') {
									result_data['partner_id'] = res[0]["partner_id"];
                                                        		io.sockets.emit("get_url", result_data);
                                       				}
                                			});
						}
						// console.log(result_data);
						io.sockets.emit("get_url", result_data);
					}
				});
			}

			var sql = "select order_id, folio_id, sale_order.state as state from hotel_room \
				join folio_room_line \
				on hotel_room.id = folio_room_line.room_id \
				join hotel_folio on folio_room_line.folio_id = hotel_folio.id \
				join sale_order on hotel_folio.order_id = sale_order.id \
				where hotel_room.cards = '" +
				data["cardno"] + "' and (state = 'sale' or state = 'draft')"
 				+ " order by hotel_folio.id desc;"

			dbconn.sqlQuery(sql, (res)=>{
				console.log(res);
				// console.log(sql);
				if (res.length !=0 && res[0] !== 'undefined') {
					result_data["order_id"] = res[0]["order_id"]
					result_data["folio_id"] = res[0]["folio_id"]
					if (data["pos"] == "true"){
						sql = "select partner_id, res_partner.name as name , sale_order.state as state from sale_order join res_partner \
							on sale_order.partner_id = res_partner.id \
							where sale_order.id='" +
							result_data["order_id"]
						dbconn.sqlQuery(sql, (res)=>{
							if (res.length != 0 && res[0] != "undefined"){
								result_data["partner_id"] = res[0]["partner_id"];
								result_data["name"] = res[0]["name"];

								io.sockets.emit("pos_customer", result_data);
							}
							
							if (data["hotel"] == "true"){
								result_data['type'] = 'folio';
								io.sockets.emit("get_url", result_data);
							}
						});
					} else {
						if (data["hotel"] == "true"){
							result_data['type'] = 'folio';
							io.sockets.emit("get_url", result_data);
						}
					}
				} else {
					sql = "select hotel_folio.order_id, hotel_folio.id as folio_id, sale_order.state as state from hotel_room \
						join hotel_reservation_line_room_rel \
						on hotel_room.id = hotel_reservation_line_room_rel.room_id \
						join hotel_reservation_line \
						on hotel_reservation_line_room_rel.hotel_reservation_line_id = hotel_reservation_line.id \
						join hotel_reservation \
						on hotel_reservation.id = hotel_reservation_line.line_id \
						join hotel_folio_reservation_rel \
						on hotel_folio_reservation_rel.order_id = hotel_reservation.id \
						join hotel_folio \
						on hotel_folio.id = hotel_folio_reservation_rel.invoice_id \
						join sale_order on hotel_folio.order_id = sale_order.id \
						where hotel_room.cards = '" +
						data["cardno"] + "' and (sale_order.state = 'sale' or sale_order.state = 'draft')"
						+ " order by hotel_folio.id desc;";

					dbconn.sqlQuery(sql, (res)=>{
						console.log(res);
						// console.log(sql);
						if (res.length ==0 || res[0] === 'undefined') return;
						result_data["order_id"] = res[0]["order_id"]
						result_data["folio_id"] = res[0]["folio_id"]
						if (data["pos"] == "true"){
							sql = "select partner_id, res_partner.name as name, sale_order.state as state \
								from sale_order join res_partner \
								on sale_order.partner_id = res_partner.id \
								where sale_order.id='" +
								result_data["order_id"]+ "'";
							console.log(sql);
							dbconn.sqlQuery(sql, (res)=>{
								console.log(res);
								if (res.length != 0 && res[0] != "undefined"){
									result_data["partner_id"] = res[0]["partner_id"];
									result_data["name"] = res[0]["name"];

									io.sockets.emit("pos_customer", result_data);
								}

								if (data["hotel"] == "true"){
									result_data['type'] = 'folio';
									io.sockets.emit("get_url", result_data);
								}
							});
						} else {
							if (data["hotel"] == "true"){
								result_data['type'] = 'folio';
								io.sockets.emit("get_url", result_data);
							}
						}
					});
				}
			});
		});
	}
}

const hotelSocketServer = new HotelCardSocketServer();

hotelSocketServer.run();
